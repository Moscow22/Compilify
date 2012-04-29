﻿using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading.Tasks;
using Compilify.Services;
using Roslyn.Scripting.CSharp;

namespace Compilify
{
    public sealed class Sandbox : IDisposable
    {
        public Sandbox(string name, byte[] compiledAssembly)
        {
            assembly = compiledAssembly;

            var evidence = new Evidence();
            evidence.AddHostEvidence(new Zone(SecurityZone.Internet));

            var permissions = SecurityManager.GetStandardSandbox(evidence);
            var security = new SecurityPermission(SecurityPermissionFlag.Execution);

            permissions.AddPermission(security);

            var setup = new AppDomainSetup
                        {
                            ApplicationBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                            PrivateBinPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                            PrivateBinPathProbe = string.Empty
                        };

            var loaderType = typeof(ByteCodeLoader);
            domain = AppDomain.CreateDomain(name, null, setup, permissions);
            loader = (ByteCodeLoader)Activator.CreateInstance(domain, loaderType.Assembly.FullName, loaderType.FullName).Unwrap();
        }
        
        private static readonly ObjectFormatter formatter = new ObjectFormatter(maxLineLength: 5120);

        private readonly byte[] assembly;
        private readonly ByteCodeLoader loader;
        private readonly AppDomain domain;
        private bool disposed;

        public string Run(string className, string resultProperty, TimeSpan timeout)
        {
            object result = null;
            var task = Task.Factory.StartNew(() => result = Execute(className, resultProperty));

            if (!task.Wait(timeout))
            {
                result = "[Execution timed out]";
            }

            return formatter.FormatObject(result) ?? "null";
        }

        public object Execute(string className, string resultProperty)
        {
            try
            {
                return loader.Run(className, resultProperty, assembly);
            }
            catch (TargetInvocationException ex)
            {
                return ex.InnerException.ToString();
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                AppDomain.Unload(domain);
            }
        }
    }
}
