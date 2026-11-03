#!/usr/bin/env bash
# Point this clone at the hooks versioned in scripts/hooks.
#
# core.hooksPath is local configuration and is not committed, so every clone
# runs the installer once. The value is relative: Git resolves it inside each
# worktree, so a linked worktree keeps its own copy of scripts/hooks.
#
# The installer refuses to replace a different core.hooksPath already set in
# this clone. Use --force only after deciding which one wins.

set -euo pipefail

hooks_dir="scripts/hooks"
min_gitleaks="8.19"

usage() {
  cat <<'TEXT'
Apunta este clon a los hooks versionados en scripts/hooks.

  scripts/install-git-hooks.sh              instala
  scripts/install-git-hooks.sh --check      falla si no está instalado o no escanea
  scripts/install-git-hooks.sh --uninstall  borra core.hooksPath de este clon
  scripts/install-git-hooks.sh --force      sustituye otro core.hooksPath

core.hooksPath es configuración local y no se versiona: hay que ejecutarlo una
vez por clon. El valor es relativo, así que cada worktree usa su copia de
scripts/hooks. Si la rama activa no tiene ese directorio, Git no ejecuta ningún
hook; --check lo detecta.

--check también falla si falta Gitleaks o es anterior a 8.19, porque entonces el
hook dejaría pasar los commits sin escanearlos o abortaría todos.
TEXT
}

find_gitleaks() {
  local found
  found="$(command -v gitleaks 2>/dev/null || true)"
  [[ -n "$found" ]] || found="$HOME/.local/bin/gitleaks"
  [[ -x "$found" ]] && printf '%s\n' "$found"
  return 0
}

gitleaks_version() {
  [[ -n "$1" ]] || return 1
  "$1" version 2>/dev/null || true
}

version_ok() {
  local version="${1:-}" major minor
  [[ "$version" =~ ^v?([0-9]+)\.([0-9]+) ]] || return 1
  major="${BASH_REMATCH[1]}"
  minor="${BASH_REMATCH[2]}"
  (( major > 8 || (major == 8 && minor >= 19) ))
}

if ! root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
  echo "Error: no estás dentro de un repositorio Git." >&2
  exit 1
fi

cd "$root"
gitleaks_path="$(find_gitleaks)"
gitleaks_ver="$(gitleaks_version "$gitleaks_path" || true)"
current="$(git config --get core.hooksPath || true)"
local_value="$(git config --local --get core.hooksPath || true)"

case "${1:-}" in
  --check)
    if [[ "$current" != "$hooks_dir" ]]; then
      echo "No instalado: core.hooksPath es '${current:-sin definir}'."
      exit 1
    fi
    if [[ ! -x "$hooks_dir/pre-commit" ]]; then
      echo "Configurado, pero $hooks_dir/pre-commit no existe o no es ejecutable."
      exit 1
    fi
    if [[ -z "$gitleaks_path" ]]; then
      echo "Instalado, pero inerte: Gitleaks no está en PATH ni en ~/.local/bin."
      exit 1
    fi
    if [[ -z "$gitleaks_ver" ]]; then
      echo "Instalado, pero inerte: no se pudo leer la versión de Gitleaks."
      exit 1
    fi
    if ! version_ok "$gitleaks_ver"; then
      echo "Instalado, pero inerte: Gitleaks $gitleaks_ver es anterior a $min_gitleaks."
      exit 1
    fi
    echo "Instalado: core.hooksPath=$hooks_dir, Gitleaks $gitleaks_ver."
    exit 0
    ;;
  --uninstall)
    if [[ "$local_value" != "$hooks_dir" ]]; then
      echo "Nada que borrar en este clon: core.hooksPath local es '${local_value:-sin definir}'."
      exit 0
    fi
    if ! git config --local --unset core.hooksPath; then
      echo "Error: no se pudo borrar core.hooksPath; revisa «git config --local --list»." >&2
      exit 1
    fi
    echo "core.hooksPath borrado de este clon."
    for scope in --global --system --worktree; do
      if value="$(git config --get "$scope" core.hooksPath 2>/dev/null)"; then
        echo "Sigue aplicando core.hooksPath ($scope): $value"
        exit 0
      fi
    done
    echo "Este clon vuelve a usar .git/hooks."
    exit 0
    ;;
  --help|-h)
    usage
    exit 0
    ;;
  --force)
    ;;
  "")
    ;;
  *)
    echo "Error: opción desconocida '$1'. Usa --check, --uninstall, --force o --help." >&2
    exit 2
    ;;
esac

[[ -f "$hooks_dir/pre-commit" ]] || { echo "Error: falta $hooks_dir/pre-commit." >&2; exit 1; }

if [[ -n "$local_value" && "$local_value" != "$hooks_dir" && "${1:-}" != "--force" ]]; then
  echo "Error: core.hooksPath ya es '$local_value' en este clon." >&2
  echo "Decide cuál manda: --force lo sustituye, --uninstall lo borra." >&2
  exit 1
fi

chmod +x "$hooks_dir/pre-commit"

default_hooks="$(git rev-parse --git-common-dir)/hooks"
if [[ -d "$default_hooks" ]]; then
  shopt -s nullglob
  displaced=()
  for f in "$default_hooks"/*; do
    [[ -f "$f" && "$f" != *.sample ]] && displaced+=("$(basename "$f")")
  done
  shopt -u nullglob
  if (( ${#displaced[@]} > 0 )); then
    echo "Aviso: estos hooks de $default_hooks dejan de ejecutarse:"
    printf '      %s\n' "${displaced[@]}"
  fi
fi

if [[ -z "$gitleaks_path" ]]; then
  echo "Aviso: Gitleaks no está en PATH ni en ~/.local/bin; el hook avisará y no escaneará."
elif [[ -z "$gitleaks_ver" ]]; then
  echo "Aviso: no se pudo leer la versión de Gitleaks; comprueba que soporta «git --staged»."
elif ! version_ok "$gitleaks_ver"; then
  echo "Aviso: Gitleaks $gitleaks_ver es anterior a $min_gitleaks; el hook abortará los commits."
fi

if [[ -n "$current" && "$current" != "$hooks_dir" ]]; then
  echo "Aviso: este clon usaba core.hooksPath=$current; pasa a $hooks_dir."
fi

git config core.hooksPath "$hooks_dir"
echo "Instalado: core.hooksPath=$hooks_dir"
