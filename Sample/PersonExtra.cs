using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NTratch.test
{
    class PersonExtra
    {
        /// <exception cref="System.AggregateException">Always throw & have a invalid xml char.</exception>
        public static void m20()
        {
            try
            {
                m500();
                m600();
                throw new AggregateException();

            } catch(InternalBufferOverflowException boex)
            {

            }
        }
        public static void m500()
        {
            //cool 
        }
        public static  void m600()
        {
            throw new InternalBufferOverflowException();

        }
        public static void m30()
        {
            try
            {
                //give me the ring! and
                m600();
                throw new ArithmeticException();
            }
            catch
            {
                // don't tell mama anything
                throw;
            }
        }
    }
}
