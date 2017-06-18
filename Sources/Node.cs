using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Misho.Cloud.MegaNz
{
    internal class NodeInfo : INodeInfo
    {
        protected NodeInfo()
        {
        }

        internal NodeInfo(string id, DownloadUrlResponse downloadResponse, byte[] key)
        {
            Id = id;
            Attributes = Crypto.DecryptAttributes(downloadResponse.SerializedAttributes.FromBase64(), key);
            Size = downloadResponse.Size;
            Type = NodeType.File;
        }

        [JsonIgnore]
        public string Name
        {
            get { return Attributes?.Name; }
        }

        [JsonProperty("s")]
        public long Size { get; protected set; }

        [JsonProperty("t")]
        public NodeType Type { get; protected set; }

        [JsonProperty("h")]
        public string Id { get; private set; }

        [JsonIgnore]
        public DateTime? ModificationDate
        {
            get { return Attributes?.ModificationDate; }
        }

        [JsonIgnore]
        public Attributes Attributes { get; protected set; }

        public bool Equals(INodeInfo other)
        {
            return other != null && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as INodeInfo);
        }
    }

    [DebuggerDisplay("Node - Type: {Type} - Name: {Name} - Id: {Id}")]
    internal class Node : NodeInfo, INode, INodeCrypto
    {
        private Node()
        {
        }

        [JsonProperty("p")]
        public string ParentId { get; private set; }

        [JsonProperty("u")]
        public string Owner { get; private set; }

        [JsonProperty("su")]
        public string SharingId { get; set; }

        [JsonProperty("sk")]
        public string SharingKey { get; set; }

        [JsonIgnore]
        public DateTime CreationDate { get; private set; }

        [JsonIgnore]
        public byte[] Key { get; private set; }

        [JsonIgnore]
        public byte[] FullKey { get; private set; }

        [JsonIgnore]
        public byte[] SharedKey { get; private set; }

        [JsonIgnore]
        public byte[] Iv { get; private set; }

        [JsonIgnore]
        public byte[] MetaMac { get; private set; }

        [JsonProperty("ts")]
        private long SerializedCreationDate { get; set; }

        [JsonProperty("a")]
        private string SerializedAttributes { get; set; }

        [JsonProperty("k")]
        private string SerializedKey { get; set; }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext ctx)
        {
            object[] context = (object[])ctx.Context;
            GetNodesResponse nodesResponse = (GetNodesResponse)context[0];
            if (context.Length == 1)
            {
                // Add key from incoming sharing.
                if (SharingKey != null && nodesResponse.SharedKeys.Any(x => x.Id == Id) == false)
                {
                    nodesResponse.SharedKeys.Add(new SharedKey(Id, SharingKey));
                }
                return;
            }
            else
            {
                byte[] masterKey = (byte[])context[1];

                CreationDate = SerializedCreationDate.ToDateTime();

                if (Type == NodeType.File || Type == NodeType.Directory)
                {
                    // There are cases where the SerializedKey property contains multiple keys separated with /
                    // This can occur when a folder is shared and the parent is shared too.
                    // Both keys are working so we use the first one
                    string serializedKey = SerializedKey.Split('/')[0];
                    int splitPosition = serializedKey.IndexOf(":", StringComparison.InvariantCulture);
                    byte[] encryptedKey = serializedKey.Substring(splitPosition + 1).FromBase64();

                    // If node is shared, we need to retrieve shared masterkey
                    if (nodesResponse.SharedKeys != null)
                    {
                        string handle = serializedKey.Substring(0, splitPosition);
                        SharedKey sharedKey = nodesResponse.SharedKeys.FirstOrDefault(x => x.Id == handle);
                        if (sharedKey != null)
                        {
                            masterKey = Crypto.DecryptKey(sharedKey.Key.FromBase64(), masterKey);
                            if (Type == NodeType.Directory)
                            {
                                SharedKey = masterKey;
                            }
                            else
                            {
                                SharedKey = Crypto.DecryptKey(encryptedKey, masterKey);
                            }
                        }
                    }

                    FullKey = Crypto.DecryptKey(encryptedKey, masterKey);

                    if (Type == NodeType.File)
                    {
                        byte[] iv, metaMac, fileKey;
                        Crypto.GetPartsFromDecryptedKey(FullKey, out iv, out metaMac, out fileKey);

                        Iv = iv;
                        MetaMac = metaMac;
                        Key = fileKey;
                    }
                    else
                    {
                        Key = FullKey;
                    }

                    Attributes = Crypto.DecryptAttributes(SerializedAttributes.FromBase64(), Key);
                }
            }
        }
    }

    [DebuggerDisplay("PublicNode - Type: {Type} - Name: {Name} - Id: {Id}")]
    internal class PublicNode : INode, INodeCrypto
    {
        private readonly Node node;

        internal PublicNode(Node node, string shareId)
        {
            this.node = node;
            ShareId = shareId;
        }

        public string ShareId { get; }

        public bool Equals(INodeInfo other)
        {
            return node.Equals(other) && ShareId == (other as PublicNode)?.ShareId;
        }

        public long Size { get { return node.Size; } }
        public string Name { get { return node.Name; } }
        public DateTime? ModificationDate { get { return node.ModificationDate; } }
        public string Id { get { return node.Id; } }
        public string ParentId { get { return node.ParentId; } }
        public string Owner { get { return node.Owner; } }
        public NodeType Type { get { return node.Type; } }
        public DateTime CreationDate { get { return node.CreationDate; } }

        public byte[] Key { get { return node.Key; } }
        public byte[] SharedKey { get { return node.SharedKey; } }
        public byte[] Iv { get { return node.Iv; } }
        public byte[] MetaMac { get { return node.MetaMac; } }
        public byte[] FullKey { get { return node.FullKey; } }
    }
}
