/*!
 *  \author  4o
 *  \brief example of IProtoFactory implementation. spawns IProtos. is called by
 *         ProtoStack in server-client mode
 */
public class PingServerFactory : IProtoFactory {
    /*!
     *  \author  4o
     *  \brief an argument for ProtoStack.RegisterProto
     */
    public bool RequireTransport {get; protected set;}

    /*!
     *  \author  4o
     *  \brief constructor...
     */
    public PingServerFactory (bool requireTransport) {
        RequireTransport = requireTransport;
    }

    /*!
     *  \author  4o
     *  \brief example of IProtoFactory.SpawnProto implementation
     */
    public IProto SpawnProto () {
        return new PingServer ();
    }
}
