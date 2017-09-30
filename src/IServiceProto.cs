/*!
 *  \author  4o
 *  \brief defines common interface for wrapper protos to check the wrapped
 *         IProto for equality.
 */
public interface IServiceProto {
    /*!
     *  \author  4o
     *  \brief checks wrapped proto for equality
     */
    bool Holds (IProto p);
}
