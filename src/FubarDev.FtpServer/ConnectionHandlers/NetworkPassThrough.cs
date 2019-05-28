// <copyright file="NetworkPassThrough.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

namespace FubarDev.FtpServer.ConnectionHandlers
{
    internal class NetworkPassThrough : CommunicationServiceBase
    {
        [NotNull]
        private readonly PipeReader _reader;

        [NotNull]
        private readonly PipeWriter _writer;

        [CanBeNull]
        private Exception _exception;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkPassThrough"/> class.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="connectionClosed">Cancellation token for a closed connection.</param>
        /// <param name="logger">The logger.</param>
        public NetworkPassThrough(
            [NotNull] PipeReader reader,
            [NotNull] PipeWriter writer,
            CancellationToken connectionClosed,
            [CanBeNull] ILogger logger = null)
            : base(connectionClosed, logger)
        {
            _reader = reader;
            _writer = writer;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                Logger?.LogTrace("Start reading from pipe");
                var readResult = await _reader.ReadAsync(cancellationToken)
                   .ConfigureAwait(false);

                var buffer = readResult.Buffer;
                var position = buffer.Start;

                while (buffer.TryGet(ref position, out var memory))
                {
                    Logger?.LogTrace("Pass through of {numBytes} bytes", memory.Length);

                    // Don't use the cancellation token source from above. Otherwise
                    // data might be lost.
                    await _writer.WriteAsync(memory, CancellationToken.None)
                       .ConfigureAwait(false);
                }

                _reader.AdvanceTo(buffer.End);

                if (readResult.IsCanceled || readResult.IsCompleted)
                {
                    Logger?.LogTrace("Cancelled={cancelled} or completed={completed}", readResult.IsCanceled, readResult.IsCompleted);
                    break;
                }
            }
        }

        /// <inheritdoc />
        protected override Task OnStopRequestedAsync(CancellationToken cancellationToken)
        {
            Logger?.LogTrace("STOP requested");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task OnPauseRequestedAsync(CancellationToken cancellationToken)
        {
            Logger?.LogTrace("PAUSE requested");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<bool> OnFailedAsync(Exception exception, CancellationToken cancellationToken)
        {
            await base.OnFailedAsync(exception, cancellationToken)
               .ConfigureAwait(false);
            _exception = exception;
            return true;
        }

        /// <inheritdoc />
        protected override Task OnPausedAsync(CancellationToken cancellationToken)
        {
            Logger?.LogTrace("PAUSED");
            return SafeFlushAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task OnStoppedAsync(CancellationToken cancellationToken)
        {
            Logger?.LogTrace("STOPPED");
            await SafeFlushAsync(cancellationToken)
               .ConfigureAwait(false);
            await OnCloseAsync(_exception, cancellationToken)
               .ConfigureAwait(false);
        }

        protected virtual Task OnCloseAsync(Exception exception, CancellationToken cancellationToken)
        {
            // Tell the PipeReader that there's no more data coming
            _reader.Complete(_exception);

            return Task.CompletedTask;
        }

        private async Task SafeFlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (_reader.TryRead(out var readResult))
                {
                    var buffer = readResult.Buffer;
                    var position = buffer.Start;

                    while (buffer.TryGet(ref position, out var memory))
                    {
                        // Don't use the cancellation token source from above. Otherwise
                        // data might be lost.
                        await _writer.WriteAsync(memory, cancellationToken)
                           .ConfigureAwait(false);
                    }

                    _reader.AdvanceTo(buffer.End);
                }
            }
            catch (Exception ex) when (ex.IsIOException())
            {
                // Ignored. Connection closed by client?
            }
            catch (Exception ex) when (ex.IsOperationCancelledException())
            {
                // Ignored. Connection closed by server?
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(0, ex, "Flush failed with: {message}", ex.Message);
            }
        }
    }
}
