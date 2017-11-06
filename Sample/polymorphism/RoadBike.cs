using System;

namespace NTratch.test.Polymorphism
{
    public class RoadBike : Bicycle{
    // In millimeters (mm)
    private int tireWidth;

    public RoadBike(int startCadence,
                    int startSpeed,
                    int startGear,
                    int newTireWidth) :
        base(startCadence,
              startSpeed,
              startGear) {
        this.setTireWidth(newTireWidth);
    }

    public int getTireWidth(){
      return this.tireWidth;
    }

    public void setTireWidth(int newTireWidth){
        this.tireWidth = newTireWidth;
    }

    public override void printDescription(){
            try
            {
                base.printDescription();

            }
            catch
            {

            }

            Console.WriteLine("The RoadBike" + " has " + getTireWidth() +
            " MM tires.");
            throw new ArithmeticException();
        }
}
}