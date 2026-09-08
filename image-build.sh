#!/usr/bin/env sh

BUILDTIME=$(date --utc +%Y%m%d%H%M%S)
VERSION_STRING=${VERSION_STRING:-0.0.0-${BUILDTIME}}
PUBLISH_OUTPUT=${PUBLISH_OUTPUT:-bin/PalServerMetricsExporter}
IMAGE_DIR=image-context
IMAGE_NAME=${IMAGE_NAME:-palserver-metrics-exporter}

if [ -d "${PUBLISH_OUTPUT}" ]; then
	echo Cleaning output directory: ${PUBLISH_OUTPUT}
	rm -rf ${PUBLISH_OUTPUT}
fi

echo Publishing dotnet application: ${VERSION_STRING}
dotnet publish \
	PalServerMetricsExporter/PalServerMetricsExporter.csproj \
	--runtime linux-x64 \
	--configuration Release \
	--property:Version=${VERSION_STRING} \
	--output ${PUBLISH_OUTPUT}
PUBLISH_RESULT=$?
if [ "${PUBLISH_RESULT}" != "0" ]; then
	echo Publish failed with exit code ${PUBLISH_RESULT}
	exit ${PUBLISH_RESULT}
fi

echo Copying publish output to image context
rm -rf ${IMAGE_DIR}/dotnet-publish
cp -a ${PUBLISH_OUTPUT} ${IMAGE_DIR}/dotnet-publish

echo Building container image: ${IMAGE_NAME}:${VERSION_STRING}
docker build \
	--build-arg VERSION_STRING=${VERSION_STRING} \
	--build-arg PUBLISH_OUTPUT=dotnet-publish \
	--tag ${IMAGE_NAME}:${VERSION_STRING} \
	-f ${IMAGE_DIR}/Dockerfile ${IMAGE_DIR}

echo Tagging image: ${IMAGE_NAME}:latest
docker tag ${IMAGE_NAME}:${VERSION_STRING} ${IMAGE_NAME}:latest

if [ "${IMAGE_PUBLISH}" = "true" ]; then
	docker push ${IMAGE_NAME}:${VERSION_STRING}
	docker push ${IMAGE_NAME}:latest
fi
