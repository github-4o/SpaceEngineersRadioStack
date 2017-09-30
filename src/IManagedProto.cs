/*!
 *  \author  4o
 *  \brief common interface for spoilable proto wrappers. abstraction leakage detected.
 */
public interface IManagedProto {
    /*!
     *  \author  4o
     *  \brief abstraction leakage: the proto defines when it has spoiled.
     *         acceptable as temporary solution while ManagedProto is the only
     *         implementation. provokes the question: why does this exist?
     */
    bool ReadyToDie {get;}
}
