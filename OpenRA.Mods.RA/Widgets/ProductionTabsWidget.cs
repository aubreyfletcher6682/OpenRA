#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.RA;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets
{
	public class ProductionTabGroup
	{
		public List<ProductionQueue> Tabs = new List<ProductionQueue>();
		public string Group;
		public bool Alert { get { return Tabs.Any(t => t.CurrentDone); } }

		public void Update(IEnumerable<ProductionQueue> allQueues)
		{
			var queues = allQueues.Where(q => q.Info.Group == Group).ToList();
			var names = new Queue<int>(Enumerable.Range(1, queues.Count).Except(queues.Select(q => q.Name)));

			// Assign names based on available numbers
			foreach (var queue in queues.Where(q => q.Name == 0))
			{
				foreach (var q in queues.Where(q => q.Name > 0))
					if (queue.Actor == q.Actor)
						queue.Name = q.Name;
				
				if (queue.Name == 0)
					queue.Name = names.Dequeue();
			}

			Tabs = queues.OrderBy(q => q.Name).ToList();

			// distinction between tabs of the same actor
			foreach (var group in queues.GroupBy(q => q.Actor))
			{
				if (group.Count() > 1)
				{
					var n = 'a';
					foreach (var queue in group)
						queue.DisplayName = queue.Name + (n++).ToString();
				}
				else
					group.First().DisplayName = group.First().Name.ToString();
 
			}
		}
	}

	public class ProductionTabsWidget : Widget
	{
		readonly World world;

		public readonly string PaletteWidget = null;
		public readonly string TypesContainer = null;
		public readonly string BackgroundContainer = null;

		public readonly int TabWidth = 30;
		public readonly int ArrowWidth = 20;
		public Dictionary<string, ProductionTabGroup> Groups;

		int contentWidth = 0;
		float listOffset = 0;
		bool leftPressed = false;
		bool rightPressed = false;
		Rectangle leftButtonRect;
		Rectangle rightButtonRect;
		Lazy<ProductionPaletteWidget> paletteWidget;
		string queueGroup;

		[ObjectCreator.UseCtor]
		public ProductionTabsWidget(World world)
		{
			this.world = world;

			Groups = world.Map.Rules.Actors.Values.SelectMany(a => a.Traits.WithInterface<ProductionQueueInfo>())
				.Select(q => q.Group).Distinct().ToDictionary(g => g, g => new ProductionTabGroup() { Group = g });

			// Only visible if the production palette has icons to display
			IsVisible = () => queueGroup != null && Groups[queueGroup].Tabs.Count > 0;

			paletteWidget = Exts.Lazy(() => Ui.Root.Get<ProductionPaletteWidget>(PaletteWidget));
		}

		public bool SelectNextTab(bool reverse)
		{
			if (queueGroup == null)
				return true;

			// Prioritize alerted queues
			var queues = Groups[queueGroup].Tabs.Select(t => t)
					.OrderByDescending(q => q.CurrentDone ? 1 : 0)
					.ToList();

			if (reverse) queues.Reverse();

			CurrentQueue = queues.SkipWhile(q => q != CurrentQueue)
				.Skip(1).FirstOrDefault() ?? queues.FirstOrDefault();

			return true;
		}

		public string QueueGroup
		{
			get
			{
				return queueGroup;
			}

			set
			{
				listOffset = 0;
				queueGroup = value;
				SelectNextTab(false);
			}
		}

		public ProductionQueue CurrentQueue
		{
			get
			{
				return paletteWidget.Value.CurrentQueue;
			}

			set
			{
				paletteWidget.Value.CurrentQueue = value;
				queueGroup = value != null ? value.Info.Group : null;

				// TODO: Scroll tabs so selected queue is visible
			}
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			leftButtonRect = new Rectangle(rb.X, rb.Y, ArrowWidth, rb.Height);
			rightButtonRect = new Rectangle(rb.Right - ArrowWidth, rb.Y, ArrowWidth, rb.Height);

			var leftDisabled = listOffset >= 0;
			var leftHover = Ui.MouseOverWidget == this && leftButtonRect.Contains(Viewport.LastMousePos);
			var rightDisabled = listOffset <= Bounds.Width - rightButtonRect.Width - leftButtonRect.Width - contentWidth;
			var rightHover = Ui.MouseOverWidget == this && rightButtonRect.Contains(Viewport.LastMousePos);

			WidgetUtils.DrawPanel("panel-black", rb);
			ButtonWidget.DrawBackground("button", leftButtonRect, leftDisabled, leftPressed, leftHover, false);
			ButtonWidget.DrawBackground("button", rightButtonRect, rightDisabled, rightPressed, rightHover, false);

			WidgetUtils.DrawRGBA(ChromeProvider.GetImage("scrollbar", leftPressed || leftDisabled ? "left_pressed" : "left_arrow"),
				new float2(leftButtonRect.Left + 2, leftButtonRect.Top + 2));
			WidgetUtils.DrawRGBA(ChromeProvider.GetImage("scrollbar", rightPressed || rightDisabled ? "right_pressed" : "right_arrow"),
				new float2(rightButtonRect.Left + 2, rightButtonRect.Top + 2));

			// Draw tab buttons
			Game.Renderer.EnableScissor(new Rectangle(leftButtonRect.Right, rb.Y + 1, rightButtonRect.Left - leftButtonRect.Right - 1, rb.Height));
			var origin = new int2(leftButtonRect.Right - 1 + (int)listOffset, leftButtonRect.Y);
			var font = Game.Renderer.Fonts["TinyBold"];
			contentWidth = 0;

			foreach (var tab in Groups[queueGroup].Tabs)
			{
				var rect = new Rectangle(origin.X + contentWidth, origin.Y, TabWidth, rb.Height);
				var hover = !leftHover && !rightHover && Ui.MouseOverWidget == this && rect.Contains(Viewport.LastMousePos);
				var baseName = tab == CurrentQueue ? "button-highlighted" : "button";
				ButtonWidget.DrawBackground(baseName, rect, false, false, hover, false);
				contentWidth += TabWidth - 1;

				var textSize = font.Measure(tab.DisplayName);
				var position = new int2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2);
				font.DrawTextWithContrast(tab.DisplayName, position, tab.CurrentDone ? Color.Gold : Color.White, Color.Black, 1);
			}

			Game.Renderer.DisableScissor();
		}

		void Scroll(int amount)
		{
			listOffset += amount * Game.Settings.Game.UIScrollSpeed;
			listOffset = Math.Min(0, Math.Max(Bounds.Width - rightButtonRect.Width - leftButtonRect.Width - contentWidth, listOffset));
		}

		// Is added to world.ActorAdded by the SidebarLogic handler
		public void ActorChanged(Actor a)
		{
			if (a.HasTrait<ProductionQueue>())
			{
				var allQueues = a.World.ActorsWithTrait<ProductionQueue>()
					.Where(p => p.Actor.Owner == p.Actor.World.LocalPlayer && p.Actor.IsInWorld && p.Trait.Enabled)
					.Select(p => p.Trait).ToArray();

				foreach (var g in Groups.Values)
					g.Update(allQueues);

				if (queueGroup == null)
					return;

				// Queue destroyed, was last of type: switch to a new group
				if (Groups[queueGroup].Tabs.Count == 0)
					QueueGroup = Groups.Where(g => g.Value.Tabs.Count > 0)
						.Select(g => g.Key).FirstOrDefault();

				// Queue destroyed, others of same type: switch to another tab
				else if (!Groups[queueGroup].Tabs.Contains(CurrentQueue))
					SelectNextTab(false);
			}
		}

		public override void Tick()
		{
			if (leftPressed) Scroll(1);
			if (rightPressed) Scroll(-1);
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			leftPressed = rightPressed = false;
			return base.YieldMouseFocus(mi);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Scroll)
			{
				Scroll(mi.ScrollDelta);
				return true;
			}

			if (mi.Button != MouseButton.Left)
				return true;

			if (mi.Event == MouseInputEvent.Down && !TakeMouseFocus(mi))
				return true;

			if (!HasMouseFocus)
				return true;

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up)
				return YieldMouseFocus(mi);

			leftPressed = leftButtonRect.Contains(mi.Location);
			rightPressed = rightButtonRect.Contains(mi.Location);
			var leftDisabled = listOffset >= 0;
			var rightDisabled = listOffset <= Bounds.Width - rightButtonRect.Width - leftButtonRect.Width - contentWidth;

			if (leftPressed || rightPressed)
			{
				if ((leftPressed && !leftDisabled) || (rightPressed && !rightDisabled))
					Sound.PlayNotification(world.Map.Rules, null, "Sounds", "ClickSound", null);
				else
					Sound.PlayNotification(world.Map.Rules, null, "Sounds", "ClickDisabledSound", null);
			}

			// Check production tabs
			var offsetloc = mi.Location - new int2(leftButtonRect.Right - 1 + (int)listOffset, leftButtonRect.Y);
			if (offsetloc.X > 0 && offsetloc.X < contentWidth)
			{
				CurrentQueue = Groups[queueGroup].Tabs[offsetloc.X / (TabWidth - 1)];
				Sound.PlayNotification(world.Map.Rules, null, "Sounds", "ClickSound", null);
			}

			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down)
				return false;

			var hotkey = Hotkey.FromKeyInput(e);

			if (hotkey == Game.Settings.Keys.NextProductionTabKey)
			{
				Sound.PlayNotification(world.Map.Rules, null, "Sounds", "ClickSound", null);
				return SelectNextTab(false);
			}
			else if (hotkey == Game.Settings.Keys.PreviousProductionTabKey)
			{
				Sound.PlayNotification(world.Map.Rules, null, "Sounds", "ClickSound", null);
				return SelectNextTab(true);
			}

			return false;
		}
	}
}
