using System;
using System.Text;
/*!
 *  \author  4o
 *  \brief example of IProto. sends ping
 */
public class PingClient : IProto {
    /*!
     *  \author  4o
     *  \brief unique IProto id.
     */
    const string protoId = "pingProtoClient";
    /*!
     *  \author  4o
     *  \brief ensures no duplicate ping messages are sent before pong is
     *         received.
     */
    bool pingSent = false;
    /*!
     *  \author  4o
     *  \brief ping messages counter. involved in stopping criterion
     */
    int pingNum = 0;
    /*!
     *  \author  4o
     *  \brief ping messages cap. involved in stopping criterion
     */
    int pingCap;
    /*!
     *  \author  4o
     *  \brief set when `pingCap` number of pings were sent
     */
    public bool Done {get {return pingNum > pingCap;}}

    /*!
     *  \author  4o
     *  \brief constructor...
     *  \param pingCap sets pingCap field
     */
    public PingClient (int pingCap) {
        this.pingCap = pingCap;
    }
    /*!
     *  \author  4o
     *  \brief Step(), which allows message sending
     */
    public StringBuilder StepCanSend () {
        if (!pingSent) {
            pingSent = true;
            return new StringBuilder ("pingProtoClient%ping");
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
     *  \brief check if incoming message is a valid pong
     */
    public void HandleMsg (string msg, int offset) {
        if (pingSent) {
            if (msg == null) {
                throw new Exception ("garbage");
            }
            if (msg.Substring (offset) == "pingProtoServer%pong") {
                pingNum++;
                pingSent = false;
            } else {
                throw new Exception ("got msg: " + msg.Substring (offset) + " offset = " + offset);
            }
        } else {
            if (msg != null) {
                throw new Exception ("garbage");
            }
        }
    }
}
