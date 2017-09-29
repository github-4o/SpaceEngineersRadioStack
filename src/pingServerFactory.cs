public class PingServerFactory : IProtoFactory {
    bool requireTransport;
    public bool RequireTransport {get {return requireTransport;}}

    public PingServerFactory (bool requireTransport) {
        this.requireTransport = requireTransport;
    }

    public IProto SpawnProto () {
        return new PingServer ();
    }
}
