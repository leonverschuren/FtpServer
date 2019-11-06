// <copyright file="S3FtpServerBuilderExtensions.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;

namespace FubarDev.FtpServer.FileSystem.S3
{
    public static class S3FtpServerBuilderExtensions
    {
        /// <summary>
        /// Uses S3 as file system.
        /// </summary>
        /// <param name="builder">The server builder used to configure the FTP server.</param>
        /// <returns>the server builder used to configure the FTP server.</returns>
        public static IFtpServerBuilder UseS3FileSystem(this IFtpServerBuilder builder)
        {
            builder.Services
                .AddSingleton<IFileSystemClassFactory, S3FileSystemProvider>();

            return builder;
        }
    }
}
