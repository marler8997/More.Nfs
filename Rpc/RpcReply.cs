using System;
using System.Collections.Generic;
using System.Text;

using More;

namespace More.Net.Rpc
{
    public enum RpcReplyStatus
    {
        Accepted = 0,
        Denied = 1,
    }
    public enum RpcAcceptStatus
    {
        Success = 0,
        ProgramUnavailable = 1,
        ProgramMismatch = 2,
        ProcedureUnavailable = 3,
        GarbageArguments = 4,
        SystemError = 5,
    }
    public enum RpcRejectStatus
    {
        RpcMismatch = 0,
        AuthenticationError = 1,
    }
    public enum RpcAuthenticationStatus
    {
        Ok = 0,
        BadCredentials = 1,
        RejectedCredentials = 2,
        BadVerifier = 3,
        RejectedVerifier = 4,
        TooWeak = 5,
        InvalidResponseVerifier = 6,
        Failed = 7,
    }
    public class RpcCallFailedException : Exception
    {
        private static String CheckForFailureInReply(RpcCall call, RpcReply reply)
        {
            if (reply.status != RpcReplyStatus.Accepted)
                return DataStringBuilder.DataString(reply.rejectedReply, new StringBuilder());

            RpcAcceptedReply acceptedReply = reply.acceptedReply;
            if (acceptedReply.status == RpcAcceptStatus.Success) return null;

            if (acceptedReply.status == RpcAcceptStatus.ProgramMismatch)
            {
                return String.Format("ProgramMismatch: {0}", DataStringBuilder.DataString(acceptedReply.mismatchInfo, new StringBuilder()));
            }
            else
            {
                return acceptedReply.status.ToString();
            }
        }
        private static String FailureReason(RpcCall call, RpcReply reply)
        {
            String failureReason = CheckForFailureInReply(call, reply);
            if (failureReason == null) throw new InvalidOperationException(
                String.Format("Expected this rpc reply '{0}' to have a failure but did not?", DataStringBuilder.DataString(reply, new StringBuilder())));
            return failureReason;
        }
        public static void VerifySuccessfulReply(RpcCall call, RpcReply reply)
        {
            String failureReason = CheckForFailureInReply(call, reply);
            if (failureReason == null) return;

            throw new RpcCallFailedException(call, failureReason);
        }

        private RpcCallFailedException(RpcCall call, String failureReason)
            : base(String.Format("{0} failed: {1}", DataStringBuilder.DataString(call, new StringBuilder()), failureReason))
        {
        }

        public RpcCallFailedException(RpcCall call, RpcReply reply)
            : this(call, FailureReason(call, reply))
        {
        }
    }
    public class RpcMismatchInfo : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(RpcMismatchInfo), "low"),
            new BigEndianUInt32Reflector(typeof(RpcMismatchInfo), "high"),
        });

        public UInt32 low,high;

        public RpcMismatchInfo()
            : base(memberSerializers)
        {
        }
        public RpcMismatchInfo(UInt32 low, UInt32 high)
            : base(memberSerializers)
        {
            this.low = low;
            this.high = high;
        }
    }
    public class RpcAcceptedReply : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new ClassFieldReflectors<RpcVerifier>(typeof(RpcAcceptedReply), "verifier", RpcVerifier.memberSerializers),
            new XdrDescriminatedUnionReflector<RpcAcceptStatus>(

                new XdrEnumReflector(typeof(RpcAcceptedReply), "status", typeof(RpcAcceptStatus)),
                
                VoidReflector.ReflectorsArray,

                new XdrDescriminatedUnionReflector<RpcAcceptStatus>.KeyAndSerializer(RpcAcceptStatus.Success, VoidReflector.ReflectorsArray),
                new XdrDescriminatedUnionReflector<RpcAcceptStatus>.KeyAndSerializer(RpcAcceptStatus.ProgramMismatch, new IReflector[] {
                    new ClassFieldReflectors<RpcMismatchInfo>(typeof(RpcAcceptedReply), "mismatchInfo", RpcMismatchInfo.memberSerializers)})

            )
        });
        public RpcVerifier verifier;
        public RpcAcceptStatus status;
        public RpcMismatchInfo mismatchInfo;

        public RpcAcceptedReply()
            : base(memberSerializers)
        {
        }
        public RpcAcceptedReply(RpcVerifier verifier)
            : base(memberSerializers)
        {
            this.verifier = verifier;
            this.status = RpcAcceptStatus.Success;
        }
        public RpcAcceptedReply(RpcVerifier verifier, RpcAcceptStatus status)
            : base(memberSerializers)
        {
            if(status == RpcAcceptStatus.Success)
                throw new ArgumentOutOfRangeException("[FunctionMisuse] This constructor is meant to create an Rpc reply to indicate an error but you passed the success value?");
            if (status == RpcAcceptStatus.ProgramMismatch)
                throw new ArgumentOutOfRangeException("[FunctionMisuse] This constructor is not meant to create a program mismatch error");

            this.verifier = verifier;
            this.status = status;
        }
    }
    public class RpcRejectedReply : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrDescriminatedUnionReflector<RpcRejectStatus>(

                new XdrEnumReflector(typeof(RpcRejectedReply), "status", typeof(RpcRejectStatus)),
                
                VoidReflector.ReflectorsArray,

                new XdrDescriminatedUnionReflector<RpcRejectStatus>.KeyAndSerializer(RpcRejectStatus.RpcMismatch, new IReflector[] {
                    new ClassFieldReflectors<RpcMismatchInfo>(typeof(RpcRejectedReply), "mismatchInfo", RpcMismatchInfo.memberSerializers)}),
                new XdrDescriminatedUnionReflector<RpcRejectStatus>.KeyAndSerializer(RpcRejectStatus.AuthenticationError, new IReflector[] {
                    new XdrEnumReflector(typeof(RpcRejectedReply), "authenticationError", typeof(RpcAuthenticationStatus))}
                )

            )
        });

        public RpcRejectStatus status;
        public RpcMismatchInfo mismatchInfo;
        public RpcAuthenticationStatus authenticationError;

        public RpcRejectedReply()
            : base(memberSerializers)
        {
        }
        public RpcRejectedReply(RpcMismatchInfo mismatchInfo)
            : base(memberSerializers)
        {
            this.status = RpcRejectStatus.RpcMismatch;
            this.mismatchInfo = mismatchInfo;
        }
    }
    public class RpcReply : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrDescriminatedUnionReflector<RpcReplyStatus>(

                new XdrEnumReflector(typeof(RpcReply), "status", typeof(RpcReplyStatus)),
                null, // No default case
                new XdrDescriminatedUnionReflector<RpcReplyStatus>.KeyAndSerializer(RpcReplyStatus.Accepted, new IReflector[] {
                    new ClassFieldReflectors<RpcAcceptedReply>(typeof(RpcReply), "acceptedReply", RpcAcceptedReply.memberSerializers)}),
                new XdrDescriminatedUnionReflector<RpcReplyStatus>.KeyAndSerializer(RpcReplyStatus.Denied, new IReflector[] {
                    new ClassFieldReflectors<RpcRejectedReply>(typeof(RpcReply), "rejectedReply", RpcRejectedReply.memberSerializers)})
            ),
        });

        public RpcReplyStatus status;
        public RpcAcceptedReply acceptedReply;
        public RpcRejectedReply rejectedReply;

        public RpcReply()
            : base(memberSerializers)
        {
        }
        public RpcReply(RpcVerifier verifier)
            : base(memberSerializers)
        {
            this.status = RpcReplyStatus.Accepted;
            this.acceptedReply = new RpcAcceptedReply(RpcVerifier.None);
        }
        public RpcReply(RpcVerifier verifier, RpcAcceptStatus acceptErrorStatus)
            : base(memberSerializers)
        {
            this.status = RpcReplyStatus.Accepted;
            this.acceptedReply = new RpcAcceptedReply(verifier, acceptErrorStatus);
        }
        public RpcReply(RpcMismatchInfo mismatchInfo)
            : base(memberSerializers)
        {
            this.status = RpcReplyStatus.Denied;
            this.rejectedReply = new RpcRejectedReply(mismatchInfo);
        }
    }
}
