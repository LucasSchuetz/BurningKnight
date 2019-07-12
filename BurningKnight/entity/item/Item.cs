using System;
using BurningKnight.assets.items;
using BurningKnight.entity.component;
using BurningKnight.entity.creature;
using BurningKnight.entity.creature.player;
using BurningKnight.entity.events;
using BurningKnight.entity.item.renderer;
using BurningKnight.entity.item.use;
using BurningKnight.entity.item.useCheck;
using BurningKnight.physics;
using BurningKnight.save;
using BurningKnight.state;
using ImGuiNET;
using Lens;
using Lens.assets;
using Lens.entity;
using Lens.graphics;
using Lens.util;
using Lens.util.file;
using Microsoft.Xna.Framework;
using VelcroPhysics.Dynamics;
using Random = Lens.util.math.Random;

namespace BurningKnight.entity.item {
	public class Item : SaveableEntity, CollisionFilterEntity {
		public ItemType Type;
		public string Id;
		public string Name => Masked ? "???" : Locale.Get(Id);
		public string Description => Locale.Get($"{Id}_desc");
		public float UseTime = 0.3f;
		public float Delay;
		public string Animation;
		public bool AutoPickup;
		public bool LoadedSelf;
		public bool Used;
		public bool Touched;
		public bool Automatic;
		
		public ItemUse[] Uses;
		public ItemUseCheck UseCheck = ItemUseChecks.Default;
		public ItemRenderer Renderer;

		public TextureRegion Region => Animation != null ? GetComponent<AnimatedItemGraphicsComponent>().Animation.GetCurrentTexture() : GetComponent<ItemGraphicsComponent>().Sprite;
		public Entity Owner => TryGetComponent<OwnerComponent>(out var o) ? o.Owner : null;
		public ItemData Data => Items.Datas[Id];
		
		public Item(ItemRenderer renderer, params ItemUse[] uses) {
			Uses = uses;
			Renderer = renderer;
			Renderer.Item = this;
		}
		
		public Item(params ItemUse[] uses) {
			Uses = uses;
		}

		public Item() {
			
		}
		
		public void Use(Entity entity) {
			if (!UseCheck.CanUse(entity, this)) {
				return;
			}

			foreach (var use in Uses) {
				try {
					use.Use(entity, this);
				} catch (Exception e) {
					Log.Error(e);
				}
			}

			Delay = Math.Abs(UseTime);

			HandleEvent(new ItemUsedEvent {
				Item = this,
				Who = entity
			});

			Used = true;
			Renderer?.OnUse();

			if (Type == ItemType.Active) {
				((Player) GetComponent<OwnerComponent>().Owner).AnimateItemPickup(this, null, false, false);
			}
		}

		public override void PostInit() {
			base.PostInit();

			if (Animation != null) {
				AddComponent(new AnimatedItemGraphicsComponent(Animation));
			} else {
				AddComponent(new ItemGraphicsComponent(Id));
			}
			
			if (LoadedSelf) {
				AddDroppedComponents();
			}
		}

		private bool Interact(Entity entity) {
			if (Masked) {
				return false;
			}
			
			if (entity.TryGetComponent<InventoryComponent>(out var inventory)) {
				inventory.Pickup(this);
				return true;
			}

			return false;
		}

		public void OnInteractionStart(Entity entity) {
			if (AutoPickup && entity.TryGetComponent<InventoryComponent>(out var inventory)) {
				if (Type != ItemType.Heart || !(entity is Creature c) || !c.GetComponent<HealthComponent>().IsFull()) {
					inventory.Pickup(this);
					entity.GetComponent<InteractorComponent>().EndInteraction();	
				}
			} else if (!HasComponent<OwnerComponent>()) {
				Engine.Instance.State.Ui.Add(new ItemPickupFx(this));
			}			
		}
		
		public virtual void AddDroppedComponents() {
			var slice = Region;
			var body = new RectBodyComponent(0, 0, slice.Source.Width, slice.Source.Height);
			
			AddComponent(body);

			body.Body.LinearDamping = 4;
			
			AddComponent(new InteractableComponent(Interact) {
				OnStart = OnInteractionStart
			});
			
			AddComponent(new ShadowComponent(RenderShadow));
			
			AddTag(Tags.LevelSave);
			AddTag(Tags.Item);
			
			AddComponent(new RoomComponent());
			AddComponent(new ExplodableComponent());
			CheckMasked();
		}

		public void RandomizeVelocity(float force) {
			if (!TryGetComponent<RectBodyComponent>(out var component)) {
				return;
			}

			force *= 60f;
			var angle = Random.AnglePI();
			
			component.Velocity += new Vector2((float) Math.Cos(angle) * force, (float) Math.Sin(angle) * force);
		}

		public virtual void RemoveDroppedComponents() {
			RemoveComponent<InteractableComponent>();
			RemoveComponent<RectBodyComponent>();
			RemoveComponent<ShadowComponent>();
			
			RemoveTag(Tags.LevelSave);
			RemoveTag(Tags.Item);

			RemoveComponent<RoomComponent>();
			RemoveComponent<ExplodableComponent>();
		}

		private void RenderShadow() {
			GraphicsComponent.Render(true);
		}

		public override void Save(FileWriter stream) {
			base.Save(stream);
			
			stream.WriteString(Id);
			stream.WriteBoolean(Used);
			stream.WriteBoolean(Touched);
			stream.WriteFloat(Delay);
			stream.WriteBoolean(Unknown);
		}

		public void ConvertTo(string id) {
			var item = Items.Create(id);

			if (item == null) {
				Log.Error($"Failed to convert item {Id}, such id does not exist!");
				return;
			}

			if (HasComponent<AnimatedItemGraphicsComponent>()) {
				RemoveComponent<AnimatedItemGraphicsComponent>();
			} else if (HasComponent<ItemGraphicsComponent>()) {
				RemoveComponent<ItemGraphicsComponent>();
			}

			Uses = item.Uses;
			UseTime = item.UseTime;
			Renderer = item.Renderer;
			Animation = item.Animation;
			AutoPickup = item.AutoPickup;
			Automatic = item.Automatic;
			Type = item.Type;
			Id = id;
			Used = false;
			
			if (Renderer != null) {
				Renderer.Item = this;
			}
			
			if (Animation != null) {
				AddComponent(new AnimatedItemGraphicsComponent(Animation));
			} else {
				AddComponent(new ItemGraphicsComponent(Id));
			}
			
			if (HasComponent<RectBodyComponent>()) {
				RemoveDroppedComponents();
				AddDroppedComponents();
			}
		}

		public override void Load(FileReader stream) {
			base.Load(stream);

			LoadedSelf = true;
			Id = stream.ReadString();

			ConvertTo(Id);

			Used = stream.ReadBoolean();
			Touched = stream.ReadBoolean();
			Delay = stream.ReadFloat();
			Unknown = stream.ReadBoolean();
		}
		
		public override void Update(float dt) {
			base.Update(dt);

			if (Type != ItemType.Active || UseTime < 0) {
				Delay = Math.Max(0, Delay - dt);
			}
		}

		public bool ShouldCollide(Entity entity) {
			return !(entity is Creature);
		}

		public override bool HandleEvent(Event e) {
			if (e is LostSupportEvent) {
				Done = true;
				return true;
			}
			
			return base.HandleEvent(e);
		}
		
		public bool Masked { get; protected set; }

		private bool unknown;

		public bool Unknown {
			get => unknown;

			set {
				unknown = value;
				CheckMasked();
			}
		}

		public void CheckMasked() {
			Masked = Unknown || (Run.Depth == 0 && !Unlocked(Id));
		}

		public static bool Unlocked(string id) {
			return GlobalSave.IsTrue(id) || id == "bk:sword" || id == "bk:lamp" || id == "bk:missile_wand" || id == "bk:gun";
		}

		public bool HandleOwnerEvent(Event e) {
			if (Uses == null) {
				return false;
			}
			
			foreach (var use in Uses) {
				if (use.HandleEvent(e)) {
					return true;
				}
			}

			return false;
		}

		#if DEBUG
		private string debugItem = "";
		
		public override void RenderImDebug() {
			if (ImGui.InputText("Item", ref debugItem, 128, ImGuiInputTextFlags.EnterReturnsTrue)) {
				ConvertTo(debugItem);
			}
		}
		#endif
	}
}