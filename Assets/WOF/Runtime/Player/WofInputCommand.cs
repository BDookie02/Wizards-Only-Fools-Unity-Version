using Unity.Netcode;
using UnityEngine;

namespace WOF
{
    public struct WofInputCommand : INetworkSerializable
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public bool Jump;
        public bool Sprint;
        public bool Slide;
        public uint Sequence;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Pitch);
            serializer.SerializeValue(ref Jump);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Slide);
            serializer.SerializeValue(ref Sequence);
        }
    }
}
