// reminder: proto structure
// ProtoStack - filteres messages by sender and receiver ID
// SessionProtoCollection - filteres messages by channel ID (aka port)
// IProto - filteres messages by proto ID

// reminder: message structure
// [receiverID] [senderId] [channelID] [protoID] [message]
//                                         ^
//                                   you are here

// this is the interface b/n SessionProtoCollection and a proto
// all classes properly implementing this interface could be associated with a
// ProtoStack via OpenSessionProto

// althogh not bligatory, it is hightly advisable to have a unique proto ID
// known proto ids:
// "transport4o" - default transport proto by despicable me
// "pingProtoServer" - test proto
// "pingProtoClient" - test proto

// if you are planning to add your protos to ProtoStack, feel free to add your
// proto ID string the section above

using System.Text;


public interface IProto {
    // spet() a proto alowing it to send a message
    StringBuilder StepCanSend ();
    // step() a proto without sending a message
    void StepCantSend ();
    // pass incoming message to proto
    void HandleMsg (string msg, int offset);
}
