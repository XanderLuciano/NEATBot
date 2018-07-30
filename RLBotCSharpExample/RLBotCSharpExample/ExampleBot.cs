using System;
using RLBotDotNet;
using rlbot.flat;
using g3;
using MathNet.Numerics;

namespace RLBotCSharpExample
{
    // We want to our bot to derive from Bot, and then implement its abstract methods.
    class ExampleBot : Bot
    {
        // We want the constructor for ExampleBot to extend from Bot, but we don't want to add anything to it.
        public ExampleBot(string botName, int botTeam, int botIndex) : base(botName, botTeam, botIndex) { }

        private Random rnd = new Random();

        private int targetBoostId = 0;
        private bool needBoost = false;
        private Vector3f[] BoostLocations = {
            new Vector3f(-3072.0, -4096.0, 73.0),
            new Vector3f(3072.0, -4096.0, 73.0),
            new Vector3f(3584, 0, 0),
            new Vector3f(-3584, 0, 0),
            new Vector3f(-3072.0, 4096.0, 73.0),
            new Vector3f(3072.0, 4096.0, 73.0),
        };

        private float CollectBoost(int boostId, Vector3 carPosition, Rotator carRotation)
        {
            Vector3f pos = new Vector3f(carPosition.X, carPosition.Y, carPosition.Z);
            Vector3f target = BoostLocations[boostId];
            double targetAngle = Math.Atan2(target.y - pos.y, target.x - pos.x);
            double botAngle = targetAngle - carRotation.Yaw;
            if (botAngle < -Math.PI)
                botAngle += Math.PI * 2;
            if (botAngle > Math.PI)
                botAngle -= Math.PI * 2;

            double angleToGo = botAngle / Math.PI;

            return Math.Min(1, (Math.Max(-1, (float)angleToGo)));
        }

        public double getDistanceToBall(Vector3 carPosition, Vector3 ballPosition)
        {
            Vector3f car = new Vector3f(carPosition.X, carPosition.Y, carPosition.Z);
            Vector3f ball = new Vector3f(ballPosition.X, ballPosition.Y, ballPosition.Z);
            return car.Distance(ball);
        }

        public double getDistanceToPoint(Vector3 carPosition, Vector3f target)
        {
            Vector3f car = new Vector3f(carPosition.X, carPosition.Y, carPosition.Z);
            return car.Distance(target);
        }

        public override Controller GetOutput(GameTickPacket gameTickPacket)
        {
            // This controller object will be returned at the end of the method.
            // This controller will contain all the inputs that we want the bot to perform.
            Controller controller = new Controller();

            // Wrap gameTickPacket retrieving in a try-catch so that the bot doesn't crash whenever a value isn't present.
            // A value may not be present if it was not sent.
            // These are nullables so trying to get them when they're null will cause errors, therefore we wrap in try-catch.
            try
            {
                // Store the required data from the gameTickPacket.
                Vector3 ballLocation = gameTickPacket.Ball.Value.Physics.Value.Location.Value;
                Vector3 carLocation = gameTickPacket.Players(this.index).Value.Physics.Value.Location.Value;
                Rotator carRotation = gameTickPacket.Players(this.index).Value.Physics.Value.Rotation.Value;

                PlayerInfo car = gameTickPacket.Players(this.index).Value;

                // Calculate to get the angle from the front of the bot's car to the ball.
                double botToTargetAngle = Math.Atan2(ballLocation.Y - carLocation.Y, ballLocation.X - carLocation.X);
                double botFrontToTargetAngle = botToTargetAngle - carRotation.Yaw;
                // Correct the angle
                if (botFrontToTargetAngle < -Math.PI)
                    botFrontToTargetAngle += 2 * Math.PI;
                if (botFrontToTargetAngle > Math.PI)
                    botFrontToTargetAngle -= 2 * Math.PI;
                
                controller.Boost = false;

                var steerAngle = 0f;

                if (car.Boost == 0 && !needBoost)
                {
                    // Go to a random boost pad
                    needBoost = true;
                    targetBoostId = rnd.Next(BoostLocations.Length - 1);
                    Console.WriteLine($"Car {this.index}: Need Boost!");
                }
                else if (car.Boost > 80)
                {
                    // Don't need boost, go for ball
                    needBoost = false;
                }

                if (needBoost)
                {
                    // Looking for boost
                    steerAngle = CollectBoost(targetBoostId, carLocation, carRotation);

                    // Find new boost pad after we hit our target
                    if (getDistanceToPoint(carLocation, BoostLocations[targetBoostId]) < 100)
                        targetBoostId = rnd.Next(BoostLocations.Length - 1);

                    // Walls tend to break things.
                    if (carLocation.Z > 200)
                        targetBoostId = rnd.Next(BoostLocations.Length - 1);
                }
                else
                {
                    // Go for ball!
                    steerAngle = botFrontToTargetAngle > 0 ? 1 : -1;
                }

                // Boost towards ball!
                controller.Boost = (getDistanceToBall(carLocation, ballLocation) < 2000);
                
                controller.Steer = steerAngle;

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            // Set the throttle to 1 so the bot can move.
            controller.Throttle = 1;

            return controller;
        }
    }
}
