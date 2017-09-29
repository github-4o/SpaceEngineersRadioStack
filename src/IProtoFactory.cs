public interface IProtoFactory {
    bool RequireTransport {get;}
    IProto SpawnProto ();
}
