﻿using DotNet.Cabinets;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;

namespace Dotnettency.TenantFileSystem
{

    public class TenantFileSystemBuilderContext<TTenant>
        where TTenant : class
    {
        private IFileProvider _parentFileProvider;

        public TenantFileSystemBuilderContext(TTenant tenant, string defaultTenantsBaseFolder)
        {
            Tenant = tenant;
            BaseFolder = defaultTenantsBaseFolder;
        }

        public TTenant Tenant { get; set; }
        public Guid PartitionId { get; set; }
        public string BaseFolder { get; set; }

        /// <summary>
        /// Chains another file provider to this tenants file system, so that the tenant can access those additional files (Read and Copy on Write access)
        /// </summary>
        /// <param name="chainedFileProvider"></param>
        /// <returns></returns>
        public TenantFileSystemBuilderContext<TTenant> AllowAccessTo(IFileProvider chainedFileProvider)
        {
            _parentFileProvider = chainedFileProvider;
            return this;
        }

        public TenantFileSystemBuilderContext<TTenant> TenantPartitionId(Guid guid)
        {
            PartitionId = guid;
            return this;
        }

        public ICabinet Build()
        {
            // Base physical folder needs to exist. This is the folder where the tenant specific folder will be created within.
            if (!Directory.Exists(BaseFolder))
            {
                Directory.CreateDirectory(BaseFolder);
            }

            var cabinetStorage = new PhysicalFileStorageProvider(BaseFolder, PartitionId);
            return new Cabinet(cabinetStorage, _parentFileProvider);
        }
    }
}
