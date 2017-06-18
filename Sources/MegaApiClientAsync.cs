using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Misho.Cloud.MegaNz
{
    public partial class MegaApiClient : IMegaApiClient
    {
        public Task<LogonSessionToken> LoginAsync(string email, string password)
        {
            return Task.Run(() => Login(email, password));
        }

        public Task<LogonSessionToken> LoginAsync(AuthInfos authInfos)
        {
            return Task.Run(() => Login(authInfos));
        }

        public Task LoginAsync(LogonSessionToken logonSessionToken)
        {
            return Task.Run(() => Login(logonSessionToken));
        }

        public Task LoginAnonymousAsync()
        {
            return Task.Run(() => LoginAnonymous());
        }

        public Task LogoutAsync()
        {
            return Task.Run(() => Logout());
        }

        public Task<IAccountInformation> GetAccountInformationAsync()
        {
            return Task.Run(() => GetAccountInformation());
        }

        public Task<IEnumerable<INode>> GetNodesAsync()
        {
            return Task.Run(() => GetNodes());
        }

        public Task<IEnumerable<INode>> GetNodesAsync(INode parent)
        {
            return Task.Run(() => GetNodes(parent));
        }

        public Task<INode> CreateFolderAsync(string name, INode parent)
        {
            return Task.Run(() => CreateFolder(name, parent));
        }

        public Task DeleteAsync(INode node, bool moveToTrash = true)
        {
            return Task.Run(() => Delete(node, moveToTrash));
        }

        public Task<INode> MoveAsync(INode sourceNode, INode destinationParentNode)
        {
            return Task.Run(() => Move(sourceNode, destinationParentNode));
        }

        public Task<INode> RenameAsync(INode sourceNode, string newName)
        {
            return Task.Run(() => Rename(sourceNode, newName));
        }

        public Task<Uri> GetDownloadLinkAsync(INode node)
        {
            return Task.Run(() => GetDownloadLink(node));
        }

        public Task<Stream> DownloadAsync(INode node, IProgress<double> progress, CancellationToken? cancellationToken = null)
        {
            return Task.Run(() =>
            {
                return (Stream)new ProgressionStream(Download(node, cancellationToken), progress, options.ReportProgressChunkSize);
            }, cancellationToken.GetValueOrDefault());
        }

        public Task<Stream> DownloadAsync(Uri uri, IProgress<double> progress, CancellationToken? cancellationToken = null)
        {
            return Task.Run(() =>
            {
                return (Stream)new ProgressionStream(Download(uri, cancellationToken), progress, options.ReportProgressChunkSize);
            }, cancellationToken.GetValueOrDefault());
        }

        public Task DownloadFileAsync(INode node, string outputFile, IProgress<double> progress, CancellationToken? cancellationToken = null)
        {
            return Task.Run(() =>
            {
                using (Stream stream = new ProgressionStream(Download(node, cancellationToken), progress, options.ReportProgressChunkSize))
                {
                    SaveStream(stream, outputFile);
                }
            }, cancellationToken.GetValueOrDefault());
        }

        public Task DownloadFileAsync(Uri uri, string outputFile, IProgress<double> progress, CancellationToken? cancellationToken = null)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrEmpty(outputFile))
                {
                    throw new ArgumentNullException("outputFile");
                }

                using (Stream stream = new ProgressionStream(Download(uri, cancellationToken), progress, options.ReportProgressChunkSize))
                {
                    SaveStream(stream, outputFile);
                }
            }, cancellationToken.GetValueOrDefault());
        }

        public Task<INode> UploadAsync(Stream stream, string name, INode parent, IProgress<double> progress, DateTime? modificationDate = null, CancellationToken? cancellationToken = null)
        {
            return Task.Run(() =>
            {
                if (stream == null)
                {
                    throw new ArgumentNullException("stream");
                }

                using (Stream progressionStream = new ProgressionStream(stream, progress, options.ReportProgressChunkSize))
                {
                    return Upload(progressionStream, name, parent, modificationDate, cancellationToken);
                }
            }, cancellationToken.GetValueOrDefault());
        }

        public Task<INode> UploadFileAsync(string filename, INode parent, IProgress<double> progress, CancellationToken? cancellationToken = null)
        {
            return Task.Run(() =>
            {
                DateTime modificationDate = File.GetLastWriteTime(filename);
                using (Stream stream = new ProgressionStream(new FileStream(filename, FileMode.Open, FileAccess.Read), progress, options.ReportProgressChunkSize))
                {
                    return Upload(stream, Path.GetFileName(filename), parent, modificationDate, cancellationToken);
                }
            }, cancellationToken.GetValueOrDefault());
        }

        public Task<INodeInfo> GetNodeFromLinkAsync(Uri uri)
        {
            return Task.Run(() => GetNodeFromLink(uri));
        }

        public Task<IEnumerable<INode>> GetNodesFromLinkAsync(Uri uri)
        {
            return Task.Run(() => GetNodesFromLink(uri));
        }
    }
}
