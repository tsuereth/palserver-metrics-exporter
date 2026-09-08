FROM mcr.microsoft.com/dotnet/runtime:10.0

ARG VERSION_STRING

# Overwrite the base container's "version" metadata label.
LABEL org.opencontainers.image.version="${VERSION_STRING}"

ARG PUBLISH_OUTPUT

COPY ${PUBLISH_OUTPUT} /PalServerMetricsExporter/
RUN chmod +x /PalServerMetricsExporter/PalServerMetricsExporter
ENTRYPOINT ["/PalServerMetricsExporter/PalServerMetricsExporter"]
