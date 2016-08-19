using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NTratch.test
{
    class Person
    {
        string name;

        public Person(string p_name)
        {
            name = p_name;
            try
            {
                m1(); // possible exceptions: COMException, and 5 from System method GetFullPath: PathTooLongException, ArgumentException, SecurityException, ArgumentNullException, NotSupportedException
                m2(); // possible exceptions: AccessViolationException, IOException
                m2(); // possible exceptions: AccessViolationException, IOException
                m3(-105); // possible exceptions: NotImplementedException
                m2(); // possible exceptions: AccessViolationException, IOException
                m30(); // pe: ---   none, it gets swallowed
            }
            catch (PathTooLongException ex)
            {
                //I'm the catch 22
                Console.WriteLine("someone tried to load a path which is too long!" + ex.Message);
            }
        }
        public void m1()
        {
            m20(); // pe: COMException
            m30(); //pe: -
            //s40 - system method that will throw something - path too long exception
            //pe: PathTooLongException
            Path.GetFullPath("I'm toooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo long for Windows File System to handle.");
        }


        /// <exception cref="System.Runtime.InteropServices.COMException">Always throw & have a invalid xml char.</exception>
        public void m20()
        {
            m500();
            m600();
            throw new COMException();
        }
        private void m500()
        {
            //cool 
        }
        private void m600()
        {
            //cool 
            
        }        
        private void m30()
        {
            try
            {
                //give me the ring! and
                throw new DivideByZeroException();
            }
            catch
            {
                // don't tell mama anything
                throw;
            }            
        }
        private void m2()
        {
            try
            {
                //give me the ring! and
                throw new AccessViolationException(); // I will escape that catch! ha!
            }
            catch (PathTooLongException ex)
            {
                Console.WriteLine("mama, she called that long file again!");
                // I will tell mama if you do that again 
            }
        }
        private int m3(int x)
        {
            if (x > 0) { return x; };
            return m3(x + 1);

            throw new NotImplementedException();
        }
    }
}
