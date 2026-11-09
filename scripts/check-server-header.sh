#!/usr/bin/env bash
# Falla si la API publica la cabecera Server, que identifica el servidor web.
# Uso: check-server-header.sh [url]
# Sale 0 si la cabecera no aparece, 1 si aparece y 2 si no se puede leer.
set -euo pipefail

url="${1:-http://localhost:8080/health}"
headers="$(curl -sS -D - -o /dev/null --max-time 10 "${url}")" || {
  echo "No se pudo leer ${url}" >&2
  exit 2
}

if printf '%s\n' "${headers}" | grep -qiE '^server:'; then
  echo "La respuesta publica la cabecera Server:" >&2
  printf '%s\n' "${headers}" | grep -iE '^server:' >&2
  exit 1
fi

echo "Sin cabecera Server en ${url}"
