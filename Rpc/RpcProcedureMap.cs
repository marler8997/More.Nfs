using System;
using System.Collections.Generic;
using System.Text;

namespace More.Net.Rpc
{
    /*
    public interface IRpcProcedureMap
    {
        RpcProcedure Lookup(UInt32 procedureNumber);
    }

    public class RpcProcedureArrayMap : IRpcProcedureMap
    {
        public readonly RpcProcedure[] procedures;

        public RpcProcedureArrayMap(RpcProcedure[] procedures)
        {
            this.procedures = procedures;
        }
        public RpcProcedure Lookup(UInt32 procedureNumber)
        {
            if (procedureNumber >= procedures.Length)
                throw new KeyNotFoundException(String.Format("Could not find procedure number {0}", procedureNumber));

            RpcProcedure procedure = procedures[procedureNumber];

            if (procedure == null)
                throw new KeyNotFoundException(String.Format("Could not find procedure number {0}", procedureNumber));

            return procedure;
        }
    }
    */
}
