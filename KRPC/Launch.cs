using KRPC.Client;
using KRPC.Client.Services.KRPC;
using KRPC.Client.Services.SpaceCenter;
using System;
using System.Threading;

namespace KRPC
{
    internal class Launch
    {
        private static void Main(string[] args)
        {
            var connection = new Connection(name: "KRPC");
            var krpc = connection.KRPC();

            var spaceCenter = connection.SpaceCenter();
            var vessel = spaceCenter.ActiveVessel;

            Launch launch = new Launch();

            launch.print("ENTER DESIRED ORBIT: ");
            string orbit = Console.ReadLine();
            launch.print("DESIRED ORBIT: " + orbit.ToString());
            launch.print("SRBs USED: ");
            string srbs = Console.ReadLine();
            bool srbsUsed = false;

            if (srbs.ToLower().Equals("y"))
                srbsUsed = true;
            else
                srbsUsed = false;

            launch.Init(vessel, connection, int.Parse(orbit.ToString()), srbsUsed);
        }

        public void Init(Vessel vessel, Connection connection, int orbit, bool srbs)
        {
            var refFrame = vessel.Orbit.Body.ReferenceFrame;
            var sc = connection.SpaceCenter();

            int intRunmode = 1;
            int intOrbitGoal = orbit;
            bool solidBoosters = srbs;
            bool burnStarted = false;
            bool staged = false;
            bool fairingsStaged = false;
            float targetPitch = 0f;

            var speedStream = connection.AddStream(() => vessel.Flight(refFrame).Speed);
            var altitudeStream = connection.AddStream(() => vessel.Flight(null).MeanAltitude);
            var apoapsisStream = connection.AddStream(() => vessel.Orbit.ApoapsisAltitude);
            var periapsisStream = connection.AddStream(() => vessel.Orbit.PeriapsisAltitude);
            var timeToAPStram = connection.AddStream(() => vessel.Orbit.TimeToApoapsis);

            print("BOOTING - " + vessel.Name);
            wait(2500);

            vessel.AutoPilot.Engage();
            vessel.AutoPilot.TargetPitchAndHeading(90, 90);
            vessel.Control.Throttle = 1;

            wait(1000);

            print("LAUNCH");
            vessel.Control.ActivateNextStage();

            while (intRunmode != 0)
            {
                if (intRunmode == 1)
                {
                    if (vessel.Flight().MeanAltitude > 50)
                    {
                        // Real Solar System Turn Equation
                        //targetPitch = (float)Math.Max(5f, (Math.Atan(900f / vessel.Flight(refFrame).Speed) * 180f / Math.PI));

                        targetPitch = (float)Math.Max(5, 90 * (1 - (vessel.Flight().MeanAltitude - 50) / (47500 - 50)));
                        vessel.AutoPilot.TargetPitch = targetPitch;

                        if (solidBoosters)
                        {
                            if (vessel.Resources.Amount("SolidFuel") < 1)
                            {
                                vessel.Control.ActivateNextStage();
                                solidBoosters = false;
                            }
                        }

                        if (vessel.AvailableThrust == 0 && !staged)
                        {
                            wait(500);
                            vessel.Control.ActivateNextStage();
                            staged = true;
                        }

                        if (vessel.Flight().MeanAltitude > 60000 && !fairingsStaged)
                        {
                            vessel.Control.ActivateNextStage();
                            fairingsStaged = true;
                        }

                        if (vessel.Orbit.ApoapsisAltitude > intOrbitGoal)
                        {
                            vessel.Control.Throttle = 0;

                            if (!staged)
                            {
                                wait(2000);
                                vessel.Control.ActivateNextStage();
                            }

                            intRunmode = 2;
                        }
                    }
                }

                if (intRunmode == 2)
                {
                    if (vessel.Flight().MeanAltitude > 60000)
                    {
                        wait(2000);

                        if (!staged)
                            vessel.Control.ActivateNextStage();

                        if (!fairingsStaged)
                            vessel.Control.ActivateNextStage();

                        intRunmode = 3;
                    }
                }

                if (intRunmode == 3)
                {
                    vessel.AutoPilot.TargetPitchAndHeading(0, 90);

                    if (vessel.Orbit.TimeToApoapsis < 12.5 && burnStarted == false)
                    {
                        vessel.Control.Throttle = 1;
                        burnStarted = true;
                    }

                    if (vessel.Orbit.PeriapsisAltitude > intOrbitGoal * 0.98)
                    {
                        vessel.Control.Throttle = 0;
                        intRunmode = 4;
                    }
                }

                if (intRunmode == 4)
                {
                    wait(2000);
                    vessel.Control.ActivateNextStage();
                    wait(5000);
                    vessel.Control.Lights = true;
                    wait(2000);
                    intRunmode = 0;
                }

                Console.Clear();
                Console.WriteLine("TARGET:          " + intOrbitGoal.ToString() + "m");
                Console.WriteLine("RUNMODE:         " + intRunmode.ToString());
                Console.WriteLine("SPEED:           " + Math.Round(speedStream.Get()) + "m/s");
                Console.WriteLine("ALTITUDE:        " + Math.Round(altitudeStream.Get()) + "m");
                Console.WriteLine("APOAPSIS:        " + Math.Round(apoapsisStream.Get()) + "m");
                Console.WriteLine("PERIAPSIS:       " + Math.Round(periapsisStream.Get()) + "m");
                Console.WriteLine("TIME TO AP:      " + Math.Round(timeToAPStram.Get()) + "s");
                Console.WriteLine("PITCH:           " + Math.Round(targetPitch).ToString() + "°");
                wait(50);
            }

            vessel.AutoPilot.Disengage();
        }

        public void print(string strMessage)
        {
            Console.Out.WriteLine(strMessage);
            wait(250);
        }

        public void wait(int intTime)
        {
            Thread.Sleep(intTime);
        }
    }
}