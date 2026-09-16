using NUnit.Framework;
using RaidSim.Core.Locomotion;
using RaidSim.Core.Mathematics;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class KinematicLocomotorTests
    {
        private const float Tolerance = 0.01f;
        private const float Speed = 5f;

        private static KinematicLocomotor MakeLocomotor(float turnRate = 0f, Vec3 start = default) =>
            new KinematicLocomotor(() => Speed, turnRate, start);

        [Test]
        public void DirectionalMove_TravelsSpeedTimesDelta()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.Move(Vec3.Forward), 1f);

            Assert.That(locomotor.Position.Z, Is.EqualTo(Speed).Within(Tolerance));
            Assert.That(locomotor.IsMoving, Is.True);
        }

        [Test]
        public void DirectionalMove_NormalisesTheInputDirection()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            // A diagonal stick reading must not be faster than a cardinal one.
            locomotor.Move(MovementIntent.Move(new Vec3(1f, 0f, 1f)), 1f);

            Assert.That(locomotor.Position.Magnitude, Is.EqualTo(Speed).Within(Tolerance));
        }

        [Test]
        public void DirectionalMove_IgnoresVerticalInput()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.Move(Vec3.Up), 1f);

            Assert.That(locomotor.Position, Is.EqualTo(Vec3.Zero));
            Assert.That(locomotor.IsMoving, Is.False);
        }

        [Test]
        public void SpeedScale_ThrottlesTravel()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.Move(Vec3.Forward, speedScale: 0.5f), 1f);

            Assert.That(locomotor.Position.Z, Is.EqualTo(Speed * 0.5f).Within(Tolerance));
        }

        [Test]
        public void IdleIntent_DoesNotMove()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.Idle, 1f);

            Assert.That(locomotor.Position, Is.EqualTo(Vec3.Zero));
            Assert.That(locomotor.IsMoving, Is.False);
        }

        [Test]
        public void ZeroDelta_DoesNotMove()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.Move(Vec3.Forward), 0f);

            Assert.That(locomotor.Position, Is.EqualTo(Vec3.Zero));
        }

        [Test]
        public void DestinationMove_StopsExactlyOnArrival_WithoutOvershooting()
        {
            KinematicLocomotor locomotor = MakeLocomotor();
            var destination = new Vec3(0f, 0f, 1f);

            // One second of travel at 5 m/s would overshoot a 1 m trip fivefold.
            locomotor.Move(MovementIntent.MoveTo(destination), 1f);

            Assert.That(locomotor.Position.Z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void DestinationMove_HonoursStoppingDistance()
        {
            KinematicLocomotor locomotor = MakeLocomotor();
            var destination = new Vec3(0f, 0f, 10f);

            for (int i = 0; i < 20; i++)
            {
                locomotor.Move(MovementIntent.MoveTo(destination, stoppingDistance: 3f), 0.2f);
            }

            Assert.That(locomotor.Position.Z, Is.EqualTo(7f).Within(Tolerance),
                "Melee AI stops at its reach rather than inside the boss.");
        }

        [Test]
        public void DestinationMove_ReportsNotMoving_OnceArrived()
        {
            KinematicLocomotor locomotor = MakeLocomotor();
            var destination = new Vec3(0f, 0f, 1f);

            locomotor.Move(MovementIntent.MoveTo(destination), 1f);
            locomotor.Move(MovementIntent.MoveTo(destination), 1f);

            Assert.That(locomotor.IsMoving, Is.False,
                "A cast that breaks on movement must not break while standing at the destination.");
        }

        [Test]
        public void DestinationMove_IgnoresHeightWhenMeasuringTheTrip()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.MoveTo(new Vec3(0f, 50f, 1f)), 1f);

            Assert.That(locomotor.Position.Y, Is.Zero, "Ground movement must not fly.");
            Assert.That(locomotor.Position.Z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void FacingFollowsTravel_ByDefault()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.Move(Vec3.Right), 0.1f);

            Assert.That(locomotor.Facing.X, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void ExplicitFacing_OverridesTheDirectionOfTravel()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            // Strafing out of a danger zone while still facing the boss.
            locomotor.Move(MovementIntent.MoveFacing(Vec3.Right, Vec3.Forward), 0.1f);

            Assert.That(locomotor.Position.X, Is.GreaterThan(0f));
            Assert.That(locomotor.Facing.Z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void FaceOnly_TurnsWithoutMoving()
        {
            KinematicLocomotor locomotor = MakeLocomotor();

            locomotor.Move(MovementIntent.FaceOnly(Vec3.Right), 1f);

            Assert.That(locomotor.Position, Is.EqualTo(Vec3.Zero));
            Assert.That(locomotor.Facing.X, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void TurnRate_LimitsHowFastFacingChanges()
        {
            KinematicLocomotor locomotor = MakeLocomotor(turnRate: 90f);

            // A 180 degree reversal at 90 deg/s takes two seconds; after one it is side-on.
            locomotor.Move(MovementIntent.FaceOnly(new Vec3(0f, 0f, -1f)), 1f);

            Assert.That(locomotor.Facing.Z, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(System.Math.Abs(locomotor.Facing.X), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void TurnToward_TakesTheShorterSide()
        {
            Vec3 result = KinematicLocomotor.TurnToward(Vec3.Forward, Vec3.Right, 90f);

            Assert.That(result.X, Is.EqualTo(1f).Within(Tolerance));

            Vec3 other = KinematicLocomotor.TurnToward(Vec3.Forward, new Vec3(-1f, 0f, 0f), 90f);

            Assert.That(other.X, Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void TurnToward_SnapsWhenTheTurnRateIsUnlimited()
        {
            Vec3 result = KinematicLocomotor.TurnToward(Vec3.Forward, Vec3.Right, 0f);

            Assert.That(result.X, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Teleport_MovesInstantlyAndClearsTheMovingFlag()
        {
            KinematicLocomotor locomotor = MakeLocomotor();
            locomotor.Move(MovementIntent.Move(Vec3.Forward), 1f);

            locomotor.Teleport(new Vec3(10f, 0f, 10f));

            Assert.That(locomotor.Position, Is.EqualTo(new Vec3(10f, 0f, 10f)));
            Assert.That(locomotor.IsMoving, Is.False);
        }
    }
}
