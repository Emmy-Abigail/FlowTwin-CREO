#!/bin/sh
# Arranque del broker: la primera vez crea el archivo de Dynamic Security con la cuenta de administrador
# (la que usa el backend para crear las cuentas de los nodos). La clave llega por variable de entorno
# desde backend/.env y nunca se escribe en el repositorio.
set -eu
ARCHIVO=/mosquitto/data/dynamic-security.json
if [ ! -f "$ARCHIVO" ]; then
  : "${MQTT_ADMIN_CLAVE:?Falta MQTT_ADMIN_CLAVE en backend/.env}"
  mosquitto_ctrl dynsec init "$ARCHIVO" "${MQTT_ADMIN_USUARIO:-flowtwin-admin}" "$MQTT_ADMIN_CLAVE"
  chown mosquitto:mosquitto "$ARCHIVO"
  echo "Dynamic Security inicializado con la cuenta ${MQTT_ADMIN_USUARIO:-flowtwin-admin}"
fi
exec /usr/sbin/mosquitto -c /mosquitto/config/mosquitto.conf
