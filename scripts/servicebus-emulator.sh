#!/usr/bin/env bash
# Arranca, comprueba y apaga el emulador de Azure Service Bus.
# Uso: servicebus-emulator.sh up|smoke|logs|status|down
# La contraseña de SQL se genera una vez y se guarda fuera de Git.
set -euo pipefail

# La contraseña de SQL nace con permisos de lectura solo para el dueño.
umask 077

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="${repo_root}/eng/servicebus-emulator/docker-compose.yml"
password_file="${repo_root}/eng/servicebus-emulator/.sql-password"
health_url="http://127.0.0.1:5300/health"

ensure_password() {
  if [[ ! -f "${password_file}" ]]; then
    # SQL Server exige 8 caracteres o más, con mayúscula, minúscula, dígito y símbolo.
    printf 'Em1!%s\n' "$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 20)" > "${password_file}"
    chmod 600 "${password_file}"
  fi
  MSSQL_SA_PASSWORD="$(cat "${password_file}")"
  export MSSQL_SA_PASSWORD
}

compose() {
  docker compose -f "${compose_file}" "$@"
}

require_docker() {
  if ! command -v docker >/dev/null 2>&1; then
    echo "Docker no está instalado o no está en PATH" >&2
    exit 2
  fi
}

wait_for_health() {
  for _ in $(seq 1 60); do
    if curl -fsS --max-time 2 "${health_url}" >/dev/null 2>&1; then
      echo "Emulador listo en ${health_url}"
      return 0
    fi
    sleep 2
  done
  echo "El emulador no respondió en ${health_url} tras 120 s" >&2
  compose logs --tail 40 >&2 || true
  return 1
}

case "${1:-}" in
  up)
    require_docker
    ensure_password
    compose up -d --wait
    wait_for_health
    ;;
  smoke)
    ensure_password
    dotnet run --project "${repo_root}/eng/servicebus-emulator/smoke" -c Release
    ;;
  logs)
    require_docker
    ensure_password
    compose logs --tail 80
    ;;
  status)
    require_docker
    ensure_password
    compose ps
    ;;
  down)
    require_docker
    ensure_password
    compose down --volumes --remove-orphans
    ;;
  *)
    echo "Uso: $0 up|smoke|logs|status|down" >&2
    exit 2
    ;;
esac
