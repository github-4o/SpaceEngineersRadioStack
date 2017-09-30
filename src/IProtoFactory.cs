/*!
 *  \author  4o
 *  \brief an IProto spawner iface to ProtoStack. used in client-server mode
 */
public interface IProtoFactory {
    /*!
     *  \author  4o
     *  \brief an argument for ProtoStack.RegisterProto
     */
    bool RequireTransport {get;}
    /*!
     *  \author  4o
     *  \brief this is called by ProtoStack when someone requested new server
     *         instance
     */
    IProto SpawnProto ();
}
