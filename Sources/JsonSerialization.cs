using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Misho.Security.Cryptography;

namespace Misho.Cloud.MegaNz
{
    internal abstract class RequestBase
    {
        protected RequestBase(string action)
        {
            Action = action;
            QueryArguments = new NameValueCollection();
        }

        [JsonProperty("a")]
        public string Action { get; private set; }

        [JsonIgnore]
        public NameValueCollection QueryArguments { get; }
    }

    internal class LoginRequest : RequestBase
    {
        public LoginRequest(string userHandle, string passwordHash)
          : base("us")
        {
            UserHandle = userHandle;
            PasswordHash = passwordHash;
        }

        [JsonProperty("user")]
        public string UserHandle { get; private set; }

        [JsonProperty("uh")]
        public string PasswordHash { get; private set; }
    }

    internal class LoginResponse
    {
        [JsonProperty("csid")]
        public string SessionId { get; private set; }

        [JsonProperty("tsid")]
        public string TemporarySessionId { get; private set; }

        [JsonProperty("privk")]
        public string PrivateKey { get; private set; }

        [JsonProperty("k")]
        public string MasterKey { get; private set; }
    }

    internal class AnonymousLoginRequest : RequestBase
    {
        public AnonymousLoginRequest(string masterKey, string temporarySession)
          : base("up")
        {
            MasterKey = masterKey;
            TemporarySession = temporarySession;
        }

        [JsonProperty("k")]
        public string MasterKey { get; set; }

        [JsonProperty("ts")]
        public string TemporarySession { get; set; }
    }

    internal class LogoutRequest : RequestBase
    {
        public LogoutRequest()
          : base("sml")
        {
        }
    }

    internal class AccountInformationRequest : RequestBase
    {
        public AccountInformationRequest()
          : base("uq")
        {
        }

        [JsonProperty("strg")]
        public int Storage { get { return 1; } }

        [JsonProperty("xfer")]
        public int Transfer { get { return 0; } }

        [JsonProperty("pro")]
        public int AccountType { get { return 0; } }
    }

    internal class AccountInformationResponse : IAccountInformation
    {
        [JsonProperty("mstrg")]
        public long TotalQuota { get; private set; }

        [JsonProperty("cstrg")]
        public long UsedQuota { get; private set; }

        [JsonProperty("cstrgn")]
        private Dictionary<string, long[]> MetricsSerialized { get; set; }

        public IEnumerable<IStorageMetrics> Metrics { get; private set; }


        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            Metrics = MetricsSerialized.Select(x => (IStorageMetrics)new StorageMetrics(x.Key, x.Value));
        }

        private class StorageMetrics : IStorageMetrics
        {
            public StorageMetrics(string nodeId, long[] metrics)
            {
                NodeId = nodeId;
                BytesUsed = metrics[0];
                FilesCount = metrics[1];
                FoldersCount = metrics[2];
            }

            public string NodeId { get; }

            public long BytesUsed { get; }

            public long FilesCount { get; }

            public long FoldersCount { get; }
        }
    }

    internal class GetNodesRequest : RequestBase
    {
        public GetNodesRequest(string shareId = null)
          : base("f")
        {
            c = 1;

            if (shareId != null)
            {
                QueryArguments["n"] = shareId;
            }
        }

        public int c { get; private set; }
    }

    internal class GetNodesResponse
    {
        public Node[] Nodes { get; private set; }

        [JsonProperty("f")]
        public JRaw NodesSerialized { get; private set; }

        [JsonProperty("ok")]
        public List<SharedKey> SharedKeys { get; private set; }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext ctx)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();

            // First Nodes deserialization to retrieve all shared keys
            settings.Context = new StreamingContext(StreamingContextStates.All, new[] { this });
            JsonConvert.DeserializeObject<Node[]>(NodesSerialized.ToString(), settings);

            // Deserialize nodes
            settings.Context = new StreamingContext(StreamingContextStates.All, new[] { this, ctx.Context });
            Nodes = JsonConvert.DeserializeObject<Node[]>(NodesSerialized.ToString(), settings);
        }
    }

    internal class DeleteRequest : RequestBase
    {
        public DeleteRequest(INode node)
          : base("d")
        {
            Node = node.Id;
        }

        [JsonProperty("n")]
        public string Node { get; private set; }
    }

    internal class GetDownloadLinkRequest : RequestBase
    {
        public GetDownloadLinkRequest(INode node)
          : base("l")
        {
            Id = node.Id;
        }

        [JsonProperty("n")]
        public string Id { get; private set; }
    }

    internal class CreateNodeRequest : RequestBase
    {
        private CreateNodeRequest(INode parentNode, NodeType type, string attributes, string encryptedKey, byte[] key, string completionHandle)
          : base("p")
        {
            ParentId = parentNode.Id;
            Nodes = new[]
            {
        new CreateNodeRequestData
        {
            Attributes = attributes,
            Key = encryptedKey,
            Type = type,
            CompletionHandle = completionHandle
        }
      };

            INodeCrypto parentNodeCrypto = parentNode as INodeCrypto;
            if (parentNodeCrypto == null)
            {
                throw new ArgumentException("parentNode node must implement INodeCrypto");
            }

            if (parentNodeCrypto.SharedKey != null)
            {
                Share = new ShareData(parentNode.Id);
                Share.AddItem(completionHandle, key, parentNodeCrypto.SharedKey);
            }
        }

        [JsonProperty("t")]
        public string ParentId { get; private set; }

        [JsonProperty("cr")]
        public ShareData Share { get; private set; }

        [JsonProperty("n")]
        public CreateNodeRequestData[] Nodes { get; private set; }

        public static CreateNodeRequest CreateFileNodeRequest(INode parentNode, string attributes, string encryptedkey, byte[] fileKey, string completionHandle)
        {
            return new CreateNodeRequest(parentNode, NodeType.File, attributes, encryptedkey, fileKey, completionHandle);
        }

        public static CreateNodeRequest CreateFolderNodeRequest(INode parentNode, string attributes, string encryptedkey, byte[] key)
        {
            return new CreateNodeRequest(parentNode, NodeType.Directory, attributes, encryptedkey, key, "xxxxxxxx");
        }

        internal class CreateNodeRequestData
        {
            [JsonProperty("h")]
            public string CompletionHandle { get; set; }

            [JsonProperty("t")]
            public NodeType Type { get; set; }

            [JsonProperty("a")]
            public string Attributes { get; set; }

            [JsonProperty("k")]
            public string Key { get; set; }
        }
    }

    internal class ShareNodeRequest : RequestBase
    {
        public ShareNodeRequest(INode node, byte[] masterKey, IEnumerable<INode> nodes)
          : base("s2")
        {
            Id = node.Id;
            Options = new object[] { new { r = 0, u = "EXP" } };

            INodeCrypto nodeCrypto = (INodeCrypto)node;
            byte[] uncryptedSharedKey = nodeCrypto.SharedKey;
            if (uncryptedSharedKey == null)
            {
                uncryptedSharedKey = Crypto.CreateAesKey();
            }

            SharedKey = Crypto.EncryptKey(uncryptedSharedKey, masterKey).ToBase64();

            if (nodeCrypto.SharedKey == null)
            {
                Share = new ShareData(node.Id);

                Share.AddItem(node.Id, nodeCrypto.FullKey, uncryptedSharedKey);

                // Add all children
                IEnumerable<INode> allChildren = GetRecursiveChildren(nodes.ToArray(), node);
                foreach (var child in allChildren)
                {
                    Share.AddItem(child.Id, ((INodeCrypto)child).FullKey, uncryptedSharedKey);
                }
            }

            byte[] handle = (node.Id + node.Id).ToBytes();
            HandleAuth = Crypto.EncryptKey(handle, masterKey).ToBase64();
        }

        private IEnumerable<INode> GetRecursiveChildren(INode[] nodes, INode parent)
        {
            foreach (var node in nodes.Where(x => x.Type == NodeType.Directory || x.Type == NodeType.File))
            {
                string parentId = node.Id;
                do
                {
                    parentId = nodes.FirstOrDefault(x => x.Id == parentId)?.ParentId;
                    if (parentId == parent.Id)
                    {
                        yield return node;
                        break;
                    }
                } while (parentId != null);
            }
        }

        [JsonProperty("n")]
        public string Id { get; private set; }

        [JsonProperty("ha")]
        public string HandleAuth { get; private set; }

        [JsonProperty("s")]
        public object[] Options { get; private set; }

        [JsonProperty("cr")]
        public ShareData Share { get; private set; }

        [JsonProperty("ok")]
        public string SharedKey { get; private set; }
    }

    internal class UploadUrlRequest : RequestBase
    {
        public UploadUrlRequest(long fileSize)
          : base("u")
        {
            Size = fileSize;
        }

        [JsonProperty("s")]
        public long Size { get; private set; }
    }

    internal class UploadUrlResponse
    {
        [JsonProperty("p")]
        public string Url { get; private set; }
    }

    internal class DownloadUrlRequest : RequestBase
    {
        public DownloadUrlRequest(INode node)
          : base("g")
        {
            Id = node.Id;

            PublicNode publicNode = node as PublicNode;
            if (publicNode != null)
            {
                QueryArguments["n"] = publicNode.ShareId;
            }
        }

        public int g { get { return 1; } }

        [JsonProperty("n")]
        public string Id { get; private set; }
    }

    internal class DownloadUrlRequestFromId : RequestBase
    {
        public DownloadUrlRequestFromId(string id)
          : base("g")
        {
            Id = id;
        }

        public int g { get { return 1; } }

        [JsonProperty("p")]
        public string Id { get; private set; }
    }

    internal class DownloadUrlResponse
    {
        [JsonProperty("g")]
        public string Url { get; private set; }

        [JsonProperty("s")]
        public long Size { get; private set; }

        [JsonProperty("at")]
        public string SerializedAttributes { get; set; }
    }

    internal class MoveRequest : RequestBase
    {
        public MoveRequest(INode node, INode destinationParentNode)
          : base("m")
        {
            Id = node.Id;
            DestinationParentId = destinationParentNode.Id;
        }

        [JsonProperty("n")]
        public string Id { get; private set; }

        [JsonProperty("t")]
        public string DestinationParentId { get; private set; }
    }

    internal class RenameRequest : RequestBase
    {
        public RenameRequest(INode node, string attributes)
          : base("a")
        {
            Id = node.Id;
            SerializedAttributes = attributes;
        }

        [JsonProperty("n")]
        public string Id { get; private set; }

        [JsonProperty("attr")]
        public string SerializedAttributes { get; set; }
    }

    public class Attributes
    {
        private const int CrcArrayLength = 4;
        private const int CrcSize = sizeof(uint) * CrcArrayLength;
        private const int FingerprintMaxSize = CrcSize + 1 + sizeof(long);
        private const int MAXFULL = 8192;
        private const uint CryptoPPCRC32Polynomial = 0xEDB88320;

        [JsonConstructor]
        private Attributes()
        {
        }

        public Attributes(string name)
        {
            Name = name;
        }

        public Attributes(string name, Attributes originalAttributes)
        {
            Name = name;
            SerializedFingerprint = originalAttributes.SerializedFingerprint;
        }

        public Attributes(string name, Stream stream, DateTime? modificationDate = null)
        {
            Name = name;

            if (modificationDate.HasValue)
            {
                byte[] fingerprintBuffer = new byte[FingerprintMaxSize];

                uint[] crc = ComputeCrc(stream);
                Buffer.BlockCopy(crc, 0, fingerprintBuffer, 0, CrcSize);

                byte[] serializedModificationDate = modificationDate.Value.ToEpoch().SerializeToBytes();
                Buffer.BlockCopy(serializedModificationDate, 0, fingerprintBuffer, CrcSize, serializedModificationDate.Length);

                Array.Resize(ref fingerprintBuffer, fingerprintBuffer.Length - (sizeof(long) + 1) + serializedModificationDate.Length);

                SerializedFingerprint = Convert.ToBase64String(fingerprintBuffer);
            }
        }

        [JsonProperty("n")]
        public string Name { get; set; }

        [JsonProperty("c", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private string SerializedFingerprint { get; set; }

        [JsonIgnore]
        public DateTime? ModificationDate
        {
            get; private set;
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            if (SerializedFingerprint != null)
            {
                var fingerprintBytes = SerializedFingerprint.FromBase64();
                ModificationDate = fingerprintBytes.DeserializeToLong(CrcSize, fingerprintBytes.Length - CrcSize).ToDateTime();
            }
        }

        private uint[] ComputeCrc(Stream stream)
        {
            // From https://github.com/meganz/sdk/blob/d4b462efc702a9c645e90c202b57e14da3de3501/src/filefingerprint.cpp

            stream.Seek(0, SeekOrigin.Begin);

            uint[] crc = new uint[CrcArrayLength];
            byte[] newCrcBuffer = new byte[CrcSize];
            uint crcVal = 0;

            if (stream.Length <= CrcSize)
            {
                // tiny file: read verbatim, NUL pad
                if (0 != stream.Read(newCrcBuffer, 0, (int)stream.Length))
                {
                    Buffer.BlockCopy(newCrcBuffer, 0, crc, 0, newCrcBuffer.Length);
                }
            }
            else if (stream.Length <= MAXFULL)
            {
                // small file: full coverage, four full CRC32s
                byte[] fileBuffer = new byte[stream.Length];
                int read = 0;
                while ((read += stream.Read(fileBuffer, read, (int)stream.Length - read)) < stream.Length) ;
                for (int i = 0; i < crc.Length; i++)
                {
                    int begin = (int)(i * stream.Length / crc.Length);
                    int end = (int)((i + 1) * stream.Length / crc.Length);

                    using (var crc32Hasher = new DamiengCrc32(CryptoPPCRC32Polynomial, DamiengCrc32.DefaultSeed))
                    {
                        crc32Hasher.TransformBlock(fileBuffer, begin, end - begin, null, 0);
                        crc32Hasher.TransformFinalBlock(fileBuffer, 0, 0);
                        var crcValBytes = crc32Hasher.Hash;
                        crcVal = BitConverter.ToUInt32(crcValBytes, 0);
                    }
                    crc[i] = crcVal;
                }
            }
            else
            {
                // large file: sparse coverage, four sparse CRC32s
                byte[] block = new byte[4 * CrcSize];
                uint blocks = (uint)(MAXFULL / (block.Length * CrcArrayLength));
                long current = 0;

                for (uint i = 0; i < CrcArrayLength; i++)
                {
                    using (var crc32Hasher = new DamiengCrc32(CryptoPPCRC32Polynomial, DamiengCrc32.DefaultSeed))
                    {
                        for (uint j = 0; j < blocks; j++)
                        {
                            long offset = (stream.Length - block.Length) * (i * blocks + j) / (CrcArrayLength * blocks - 1);

                            stream.Seek(offset - current, SeekOrigin.Current);
                            current += (offset - current);

                            int blockWritten = stream.Read(block, 0, block.Length);
                            current += blockWritten;
                            crc32Hasher.TransformBlock(block, 0, blockWritten, null, 0);
                        }

                        crc32Hasher.TransformFinalBlock(block, 0, 0);
                        var crc32ValBytes = crc32Hasher.Hash;
                        crcVal = BitConverter.ToUInt32(crc32ValBytes, 0);

                    }
                    crc[i] = crcVal;
                }
            }

            return crc;
        }
    }

    [JsonConverter(typeof(ShareDataConverter))]
    internal class ShareData
    {
        private IList<ShareDataItem> items;

        public ShareData(string nodeId)
        {
            NodeId = nodeId;
            items = new List<ShareDataItem>();
        }

        public string NodeId { get; private set; }

        public IEnumerable<ShareDataItem> Items { get { return items; } }

        public void AddItem(string nodeId, byte[] data, byte[] key)
        {
            ShareDataItem item = new ShareDataItem
            {
                NodeId = nodeId,
                Data = data,
                Key = key
            };

            items.Add(item);
        }

        public class ShareDataItem
        {
            public string NodeId { get; set; }

            public byte[] Data { get; set; }

            public byte[] Key { get; set; }
        }
    }

    internal class ShareDataConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ShareData data = value as ShareData;
            if (data == null)
            {
                throw new ArgumentException("invalid data to serialize");
            }

            writer.WriteStartArray();

            writer.WriteStartArray();
            writer.WriteValue(data.NodeId);
            writer.WriteEndArray();

            writer.WriteStartArray();
            foreach (var item in data.Items)
            {
                writer.WriteValue(item.NodeId);
            }
            writer.WriteEndArray();

            writer.WriteStartArray();
            int counter = 0;
            foreach (var item in data.Items)
            {
                writer.WriteValue(0);
                writer.WriteValue(counter++);
                writer.WriteValue(Crypto.EncryptKey(item.Data, item.Key).ToBase64());
            }
            writer.WriteEndArray();

            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ShareData);
        }
    }

    [DebuggerDisplay("Id: {Id} / Key: {Key}")]
    internal class SharedKey
    {
        public SharedKey(string id, string key)
        {
            Id = id;
            Key = key;
        }

        [JsonProperty("h")]
        public string Id { get; private set; }

        [JsonProperty("k")]
        public string Key { get; private set; }
    }
}
