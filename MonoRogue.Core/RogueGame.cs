using Microsoft.Xna.Framework;
using RogueSharp;
using RogueSharp.Random;
using System;
using Game = Microsoft.Xna.Framework.Game;

namespace MonoRogue.Core
{
    public class RogueGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        public RogueGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _graphics.GraphicsDevice.Clear(Color.Black);

            base.Draw(gameTime);
        }
    }

    
}