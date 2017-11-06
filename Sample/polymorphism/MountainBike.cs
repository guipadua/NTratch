using System;

namespace NTratch.test.Polymorphism
{

    public class MountainBike : Bicycle {
        private string suspension;

        public MountainBike(
                   int startCadence,
                   int startSpeed,
                   int startGear,
                   string suspensionType): 
            base(startCadence,
                  startSpeed,
                  startGear)
        { 
            this.setSuspension(suspensionType);
        }

        public string getSuspension(){
          return this.suspension;
        }

        public void setSuspension(string suspensionType) {
            this.suspension = suspensionType;
        }

        public override void printDescription() {
            try
            {
                base.printDescription();

            }
            catch
            {

            }

            Console.WriteLine("The " + "MountainBike has a" +
                getSuspension() + " suspension.");
            throw new AggregateException();
        }
    } 
}