#!/usr/bin/env python3
"""Rechaza un nombre de recurso escrito a mano en la plantilla de infraestructura.

La regla está en `docs/adr/0011-nombres-de-recurso-caf.md`: el nombre de cada
recurso sale del objeto `names`, que lo calcula `resourceName`. Solo se admiten
las excepciones que fija Azure: las dos zonas DNS privadas y el nombre `logs` de
la configuración del App Service; el `guid(...)` de una asignación de rol es un
identificador determinista, no un nombre de recurso. Cualquier otro valor en el
`name` de primer nivel de un recurso hace fallar la comprobación.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path
from typing import Iterator

DECLARATION = re.compile(
    r"^[ \t]*resource\s+[A-Za-z_][A-Za-z0-9_]*\s+'(?P<type>[^']+)'",
    re.MULTILINE,
)
IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
NAME_PROPERTY = re.compile(r"names\.[A-Za-z_][A-Za-z0-9_]*")
NAMES_OBJECT = re.compile(r"^[ \t]*var\s+names\s*=\s*\{", re.MULTILINE)
NAMING_FUNCTION = "resourceName("

# Variables cuyo valor impone Azure, no la plantilla.
ALLOWED_VARIABLES = {"keyVaultDnsZoneName", "appServiceDnsZoneName"}
# Pares (tipo de recurso, literal) que también fija la plataforma.
ALLOWED_LITERALS = {("Microsoft.Web/sites/config", "'logs'")}
# Pares (tipo de recurso, prefijo de expresión) que no son nombres de recurso.
ALLOWED_EXPRESSIONS = {("Microsoft.Authorization/roleAssignments", "guid(")}


def skip_string(text: str, start: int) -> int:
    """Devuelve el índice posterior a la cadena que abre en `start`."""
    i = start + 1
    while i < len(text):
        if text[i] == "\\":
            i += 2
            continue
        if text[i] == "'":
            if text.startswith("''", i):
                i += 2
                continue
            return i + 1
        i += 1
    raise ValueError(f"cadena sin cerrar en el carácter {start}")


def strip_block_comments(text: str) -> str:
    """Sustituye los comentarios `/* ... */` por espacios, conservando las líneas."""
    result: list[str] = []
    i = 0
    while i < len(text):
        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            if end == -1:
                raise ValueError("comentario de bloque sin cerrar")
            result.append("\n" * text.count("\n", i, end + 2))
            i = end + 2
            continue
        if text.startswith("//", i):
            end = text.find("\n", i)
            end = len(text) if end == -1 else end
            result.append(text[i:end])
            i = end
            continue
        if text[i] == "'":
            end = skip_string(text, i)
            result.append(text[i:end])
            i = end
            continue
        result.append(text[i])
        i += 1
    return "".join(result)


def read_expression(text: str, start: int) -> tuple[str, int]:
    """Lee el valor de una propiedad hasta el fin de la línea, con paréntesis balanceados."""
    i = start
    while i < len(text) and text[i] in " \t\r\n":
        i += 1
    begin = i
    depth = 0
    while i < len(text):
        char = text[i]
        if char == "'":
            i = skip_string(text, i)
            continue
        if text.startswith("//", i):
            newline = text.find("\n", i)
            i = len(text) if newline == -1 else newline
            continue
        if char in "([{":
            depth += 1
            i += 1
            continue
        if char in ")]}":
            if depth == 0:
                break
            depth -= 1
            i += 1
            continue
        if char in "\r\n" and depth == 0:
            break
        i += 1
    value = " ".join(text[begin:i].split())
    return value, i


def iter_properties(text: str, start: int) -> Iterator[tuple[str, str, int]]:
    """Recorre las propiedades de primer nivel de un bloque que abre en `start`."""
    depth = 1
    i = start + 1
    while i < len(text) and depth > 0:
        char = text[i]
        if char == "'":
            i = skip_string(text, i)
            continue
        if text.startswith("//", i):
            newline = text.find("\n", i)
            i = len(text) if newline == -1 else newline + 1
            continue
        if char in "{[(":
            depth += 1
            i += 1
            continue
        if char in "}])":
            depth -= 1
            i += 1
            continue
        if depth == 1 and (char.isalpha() or char == "_"):
            match = IDENTIFIER.match(text, i)
            if match is None:
                i += 1
                continue
            after = match.end()
            while after < len(text) and text[after] in " \t":
                after += 1
            if after < len(text) and text[after] == ":":
                line = text.count("\n", 0, i) + 1
                value, end = read_expression(text, after + 1)
                yield match.group(0), value, line
                i = end
                continue
            i = match.end()
            continue
        i += 1


def body_start(text: str, start: int) -> int | None:
    """Devuelve el índice de la llave que abre el cuerpo de un recurso."""
    i = start
    parentheses = 0
    while i < len(text):
        char = text[i]
        if char == "'":
            i = skip_string(text, i)
            continue
        if text.startswith("//", i):
            newline = text.find("\n", i)
            i = len(text) if newline == -1 else newline + 1
            continue
        if char == "(":
            parentheses += 1
            i += 1
            continue
        if char == ")":
            parentheses -= 1
            i += 1
            continue
        if char == "{" and parentheses == 0:
            return i
        i += 1
    return None


def check_names_object(text: str, path: Path) -> list[str]:
    match = NAMES_OBJECT.search(text)
    if match is None:
        # Un módulo puede recibir los nombres por parámetro; sus recursos siguen
        # pasando por la comprobación de `name`.
        return []
    problems: list[str] = []
    declared = 0
    for key, value, line in iter_properties(text, match.end() - 1):
        declared += 1
        if not value.startswith(NAMING_FUNCTION):
            problems.append(
                f"{path}:{line}: `names.{key}` no llama a `resourceName`; "
                f"el nombre volvería a escribirse a mano (ADR-0011)."
            )
    if declared == 0:
        problems.append(f"{path}: el objeto `names` está vacío.")
    return problems


def check_file(path: Path) -> list[str]:
    text = strip_block_comments(path.read_text(encoding="utf-8"))
    problems: list[str] = []
    declared = 0
    for match in DECLARATION.finditer(text):
        declared += 1
        resource_type = match.group("type").split("@", 1)[0]
        line = text.count("\n", 0, match.start()) + 1
        start = body_start(text, match.end())
        if start is None:
            problems.append(f"{path}:{line}: no se encontró el cuerpo del recurso.")
            continue
        name = next(
            (prop for prop in iter_properties(text, start) if prop[0].lower() == "name"),
            None,
        )
        if name is None:
            problems.append(f"{path}:{line}: el recurso no declara `name` en el primer nivel.")
            continue
        value, value_line = name[1], name[2]
        if NAME_PROPERTY.fullmatch(value):
            continue
        if value in ALLOWED_VARIABLES:
            continue
        if (resource_type, value) in ALLOWED_LITERALS:
            continue
        if any(
            resource_type == allowed_type and value.startswith(prefix)
            for allowed_type, prefix in ALLOWED_EXPRESSIONS
        ):
            continue
        problems.append(
            f"{path}:{value_line}: `{value}` está escrito a mano; usa `names.<clave>` "
            f"(ADR-0011) o declara la excepción de la plataforma."
        )
    if declared == 0 and re.search(r"^[ \t]*resource\b", text, re.MULTILINE):
        problems.append(f"{path}: no se pudo leer ninguna declaración `resource`.")
    problems.extend(check_names_object(text, path))
    return problems


def main(argv: list[str]) -> int:
    args = argv[1:] or ["infra"]
    paths: list[Path] = []
    for arg in args:
        path = Path(arg)
        if path.is_dir():
            paths.extend(sorted(path.rglob("*.bicep")))
        else:
            paths.append(path)
    if not paths:
        print(f"error: no hay plantillas .bicep en {', '.join(args)}", file=sys.stderr)
        return 2
    problems: list[str] = []
    for path in paths:
        try:
            problems.extend(check_file(path))
        except (OSError, ValueError) as error:
            print(f"error: {error}", file=sys.stderr)
            return 2
    if problems:
        print("\n".join(problems), file=sys.stderr)
        return 1
    print(
        f"{', '.join(str(path) for path in paths)}: todos los nombres salen de "
        f"`resourceName` o de una excepción de la plataforma."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
