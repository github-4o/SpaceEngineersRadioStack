using System;
using System.Text;
/*!
 *  \author  4o
 *  \brief example of IProto. sends pong messages in reply for pings
 */
public class PingServer : IProto {
    /*!
     *  \author  4o
     *  \brief unique IProto id.
     */
    const string protoId = "pingProtoServer";
    /*!
     *  \author  4o
     *  \brief indicates there is a pong scheduled for sending. set when
     *         PingServer receives ping message via HandleMsg(). unset in
     *         StepCanSend().
     */
    bool sendPong = false;
    /*!
     *  \author  4o
     *  \brief Step(), which allows message sending
     */
    public StringBuilder StepCanSend () {
        if (sendPong) {
            sendPong = false;
            return new StringBuilder ("pingProtoServer%pong");
        }
        return null;
    }
    /*!
     *  \author  4o
     *  \brief Step(), which forbids message sending. nothing to do here.
     */
    public void StepCantSend () {}
    /*!
     *  \author  4o
     *  \brief check if incoming message is a valid ping
     */
    public void HandleMsg (string msg, int offset) {
        if (msg == null) {
            throw new Exception ("null msg");
        }
        if (sendPong) {
            return;
        }
        if (msg.Substring (offset) == "pingProtoClient%ping") {
            sendPong = true;
        } else {
            throw new Exception ("invalid msg: " + msg.Substring (offset) + " offset = " + offset);
        }
    }
}
