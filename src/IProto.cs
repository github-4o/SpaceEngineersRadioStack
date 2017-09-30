using System.Text;

/*!
 *  \author  4o
 *  \brief this is the interface b/n SessionProtoCollection and a particular
 *         proto. user is encouraged to create their own implementations
 *  \details <BR>reminder: proto structure
 *           <BR>ProtoStack - filters messages by sender and receiver ID
 *           <BR>SessionProtoCollection - filters messages by channel ID (aka port)
 *           <BR>IProto - filters messages by proto ID
 *           <BR><BR>reminder: message structure
 *           <BR>[receiverID] [senderId] [channelID] [protoID] [message]
 *           <BR>                                        ^
 *           <BR>                                  you are here
 *           <BR><BR>this is the interface b/n SessionProtoCollection and a proto
 *           all classes properly implementing this interface could be associated with a
 *           ProtoStack via OpenSessionProto
 *           <BR><BR>although not obligatory, it is highly advisable to have a unique proto ID
 *           known proto ids:
 *           <BR>"transport4o" - default transport proto by despicable me
 *           <BR>"pingProtoServer" - test proto
 *           <BR>"pingProtoClient" - test proto
 *           <BR><BR>if you are planning to add your protos to ProtoStack, feel free to add your
 *           proto ID string the section above
 */
public interface IProto {
    /*!
     *  \author  4o
     *  \brief step() a proto allowing it to send a message
     */
    StringBuilder StepCanSend ();
    /*!
     *  \author  4o
     *  \brief step() a proto without sending a message
     */
    void StepCantSend ();
    /*!
     *  \author  4o
     *  \brief pass incoming message to proto
     */
    void HandleMsg (string msg, int offset);
}
