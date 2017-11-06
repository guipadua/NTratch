using System;

namespace NTratch.test.Polymorphism
{

public class Program {
		static void Main(string[] args)
		{
			Bicycle bike01, bike02, bike03;

		bike01 = new Bicycle(20, 10, 1);
		bike02 = new MountainBike(20, 10, 5, "Dual");
		bike03 = new RoadBike(40, 20, 8, 23);

		try{
			bike01.printDescription();
			bike02.printDescription();
			bike03.printDescription();
		} catch{
			//empty block
		}
		
		try{
			bike02.printDescription();
			
		} catch{
			//empty block
		}
		
		try{
			bike03.printDescription();
		} catch{
			//empty block
		}

			Console.ReadKey();  
		}
	}
}