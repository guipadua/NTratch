using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NTratch.test
{
    class Person
    {
        string name;

        public void m0()
        {
            try
            {
                m1(); // possible exceptions: COMException, and 5 from System method GetFullPath: PathTooLongException, ArgumentException, SecurityException, ArgumentNullException, NotSupportedException
                m2(); // possible exceptions: AccessViolationException, IOException
                m3(); // possible exceptions: NotImplementedException                
            }
            catch (PathTooLongException ex)
            {
                logger.Error("Path Too Long!", ex);                
            } 
        }

        public void m1()
        {
            m20(); // pe: COMException
            m30(); //pe: 0
            //pe: PathTooLongException
            Path.GetFullPath("I'm toooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo long for Windows File System to handle.");
        }
        
        /// <exception cref="System.Runtime.InteropServices.COMException">Always throw.</exception>
        public void m20()
        {
            m500();
            m600();
            throw new COMException();
        }               
        private void m30()
        {
            try
            {
                throw new DivideByZeroException();
            }
            catch //Generic catch
            {
               //empty
            }            
        }
        private void m2()
        {
            try
            {
                throw new AccessViolationException();
            }
            catch (PathTooLongException ex)
            {
                throw; 
            }
        }
        private int m3()
        {
            throw new NotImplementedException();
        }
    }
}
