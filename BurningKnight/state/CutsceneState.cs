using BurningKnight.assets;
using BurningKnight.debug;
using BurningKnight.entity.component;
using BurningKnight.entity.cutscene.controller;
using BurningKnight.physics;
using BurningKnight.ui.imgui;
using BurningKnight.util;
using Lens;
using Lens.entity;
using Lens.game;
using Lens.graphics;
using Lens.graphics.gamerenderer;
using Lens.util.camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace BurningKnight.state {
	public class CutsceneState : GameState {
		public Console Console;
		public Camera Camera;
		
		public CutsceneState(Area area) {
			Area = area;
		}
		
		public override void Init() {
			base.Init();

			Shaders.Ui.Parameters["black"].SetValue(1f);

			Area.Add(new GobboCutsceneController());
			Ui.Add(Camera = new Camera(new FollowingDriver()));
			Console = new Console(Area);
			
			Camera.Position = new Vector2(Display.Width / 2f, Display.Height / 2f);
		}
		
		private void PrerenderShadows() {
			var renderer = (PixelPerfectGameRenderer) Engine.Instance.StateRenderer;

			renderer.EnableClip = false;
			renderer.End();
			renderer.BeginShadows();

			foreach (var e in Area.Tagged[Tags.HasShadow]) {
				if (e.AlwaysVisible || e.OnScreen) {
					e.GetComponent<ShadowComponent>().Callback();
				}
			}
			
			renderer.EndShadows();
			renderer.Begin();
		}

		public override void Render() {
			PrerenderShadows();
			base.Render();
			Physics.Render();
		}

		public override void RenderNative() {
			ImGuiHelper.Begin();
			Console.Render();
			WindowManager.Render(Area);
			ImGuiHelper.End();			
			
			Graphics.Batch.Begin();
			Graphics.Batch.DrawCircle(new CircleF(Mouse.GetState().Position, 3f), 8, Color.White);
			Graphics.Batch.End();
		}
	}
}