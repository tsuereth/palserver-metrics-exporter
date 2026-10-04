using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PalServerMetricsExporter
{
    public class PalworldMapServer : IDisposable
    {
        private readonly ILogger logger;
        private readonly byte[] mapFileBytes;
        private readonly string mapFileContentType;
        private readonly string serveMapPath;
        private readonly HttpListener listener;

        private bool disposed;

        public PalworldMapServer(
            ILogger logger,
            string sourceMapFilePath,
            string sourceMapFileContentType,
            string bindHost,
            ushort bindPort,
            string serveMapPath)
        {
            this.logger = logger;

            // If there's an exception reading the map file, let it explode. :)
            this.logger.LogInformation($"Map Server using source file: {sourceMapFilePath}");
            this.mapFileBytes = File.ReadAllBytes(sourceMapFilePath);
            this.mapFileContentType = sourceMapFileContentType;

            // Quirk notes:
            // - .NET's HttpListener rejects the well-known "0.0.0.0" bind host address;
            //   it instead expects the hostname "*" when binding to all local hostnames/addrs.
            // - The underlying HttpListener requires a trailing '/' on its "prefix" parameter,
            //   even though that trailing '/' isn't a literal requirement for client requests.
            var normalizedBindHost = bindHost;
            if (normalizedBindHost.Equals("0.0.0.0", StringComparison.Ordinal))
            {
                normalizedBindHost = "*";
            }
            var normalizedMapPath = serveMapPath;
            if (!normalizedMapPath.EndsWith('/'))
            {
                normalizedMapPath += '/';
            }

            this.logger.LogInformation($"Map Server serving maps at: http://{bindHost}:{bindPort}{serveMapPath}");
            this.serveMapPath = normalizedMapPath;
            this.listener = new HttpListener();
            this.listener.Prefixes.Add($"http://{normalizedBindHost}:{bindPort}{normalizedMapPath}");
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    // Fun fact: HttpListener's Dispose() implementation is private?
                    // But its public Close() method will include disposal internally.
                    this.listener.Close();

                    this.disposed = true;
                }
            }
        }

        public async Task ServeAsync(CancellationToken cancelToken)
        {
            this.listener.Start();
            while (!cancelToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cancelToken);
                var request = context.Request;
                var response = context.Response;

                var normalizedRequestPath = request.Url.LocalPath;
                if (!normalizedRequestPath.EndsWith('/'))
                {
                    normalizedRequestPath += '/';
                }
                if (normalizedRequestPath.Equals(this.serveMapPath, StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentLength64 = this.mapFileBytes.Length;
                    response.ContentType = this.mapFileContentType;
                    using (var responseWriter = new BinaryWriter(response.OutputStream))
                    {
                        responseWriter.Write(this.mapFileBytes, 0, this.mapFileBytes.Length);
                        responseWriter.Close();
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }

                response.Close();
            }
        }
    }
}
