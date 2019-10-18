﻿using FlecsSharp;
using System;

namespace HelloWorld
{
    class Program
    {
        public struct Position
        {
            public float X { get; set; }
            public float Y { get; set; }
        }

        public struct Speed
        {
            public int SpeedValue { get; set; }
        }

        static void PositionSystem(ref Rows rows, Span<Position> position)
        {
            for (int i = 0; i < (int)rows.Count; i++)
            {
                EntityId id = rows[i];
                position[i].X += 1;
                position[i].Y += 1;
            }
        }

        static void MoveSystem(ref Rows rows, Span<Position> position, Span<Speed> speed)
        {
            for(int i = 0; i < (int)rows.Count; i++)
            {
                EntityId id = rows[i];
                position[i].X += speed[i].SpeedValue * rows.DeltaTime;
                position[i].Y += speed[i].SpeedValue * rows.DeltaTime;
            }
        }


        static void Main(string[] args)
        {
            using (var world = World.Create())
            {
                world.AddSystem<Position, Speed>(MoveSystem, SystemKind.OnUpdate);
                world.AddSystem<Position>(PositionSystem, SystemKind.OnUpdate);

                var myEntity = world.NewEntity("MyEntity1", new Position { X = 1, Y = 2 }, new Speed { SpeedValue = 5 });
                world.NewEntity("MyEntity2", new Position { X = 1, Y = 2 }, new Speed { SpeedValue = 5 });

                world.NewEntity("MyEntity3", new Position { X = 1, Y = 2 });
                world.NewEntity("MyEntity4", new Position { X = 1, Y = 2 });

                var i = 0;
                while (world.Progress(1))
                {
                    if (i++ > 10)
                        break;
                }
            }
        }

    }
}