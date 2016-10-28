using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NTratch.test
{
    class Person
    {
        string name;

        public void NonGenericCatch()
        {
            try
            {
                Method1();                           
            }
            catch (PathTooLongException ex)
            {
                logger.Error("Path Too Long!", ex);
            } 
        }

        public void GenericCatch()
        {
            try
            {
                Method1();                
            }
            catch (Exception ex)
            {
                logger.Error("Exception happened!", ex);
            }
        }

        public void Method1()
        {
            Method20();
            // [PathTooLongException Semantic Doc]:
            Path.GetFullPath(very_long_string);
        }
        
        /// <exception cref=
        /// "System.Runtime.InteropServices.COMException">
        /// [COMException Syntax Doc].</exception>
        public void Method20()
        {
            // [COMException Syntax Throw]:
            throw new COMException();
        }   
        
                    
        private void Method30()
        {
            try
            {
                throw new DivideByZeroException();
            }
            catch 
            {
               
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
