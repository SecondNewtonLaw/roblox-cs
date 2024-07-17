﻿using Roblox;
using Components;

namespace TestGame.Client
{
    public class LavaComponent : GameComponent<Part>
    {
        public new Part Instance { get; set; }

        public LavaComponent(Part instance)
            : base(instance)
        {
            Instance = instance;
        }

        public override void Start()
        {
            Instance.Touched.Connect(hit =>
            {
                var model = hit.FindFirstAncestorOfClass<Model>();
                var humanoid = model?.FindFirstChildOfClass<Humanoid>();
                if (humanoid == null) return;

                humanoid.TakeDamage(10);
            });
        }

        public override void Update(double dt)
        {
        }

        public override void Destroy()
        {
        }
    }
}