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
using System.Drawing;
using OpenRA.FileFormats;
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Widgets
{
	public class VqaPlayerWidget : Widget
	{
		public Hotkey CancelKey = new Hotkey(Keycode.ESCAPE, Modifiers.None);
		public float AspectRatio = 1.2f;
		public bool DrawOverlay = true;

		public bool Paused { get { return paused; } }
		public VqaReader Video { get { return video; } }

		Sprite videoSprite, overlaySprite;
		VqaReader video = null;
		string cachedVideo;
		float invLength;
		float2 videoOrigin, videoSize;
		uint[,] overlay;
		bool stopped;
		bool paused;

		Action onComplete;

		readonly World world;

		[ObjectCreator.UseCtor]
		public VqaPlayerWidget(World world)
		{
			this.world = world;
		}

		public void Load(string filename)
		{
			if (filename == cachedVideo)
				return;

			stopped = true;
			paused = true;
			Sound.StopVideo();
			onComplete = () => { };

			cachedVideo = filename;
			video = new VqaReader(GlobalFileSystem.Open(filename));

			invLength = video.Framerate * 1f / video.Frames;

			var size = Math.Max(video.Width, video.Height);
			var textureSize = Exts.NextPowerOf2(size);
			var videoSheet = new Sheet(new Size(textureSize, textureSize), false);

			videoSheet.Texture.ScaleFilter = TextureScaleFilter.Linear;
			videoSheet.Texture.SetData(video.FrameData);
			videoSprite = new Sprite(videoSheet, new Rectangle(0, 0, video.Width, video.Height), TextureChannel.Alpha);

			var scale = Math.Min(RenderBounds.Width / video.Width, RenderBounds.Height / (video.Height * AspectRatio));
			videoOrigin = new float2(RenderBounds.X + (RenderBounds.Width - scale * video.Width) / 2, RenderBounds.Y + (RenderBounds.Height - scale * AspectRatio * video.Height) / 2);

			// Round size to integer pixels. Round up to be consistent with the scale calcuation.
			videoSize = new float2((int)Math.Ceiling(video.Width * scale), (int)Math.Ceiling(video.Height * scale * AspectRatio));

			if (!DrawOverlay)
				return;

			var scaledHeight = (int)videoSize.Y;
			overlay = new uint[Exts.NextPowerOf2(scaledHeight), 1];
			var black = (uint)255 << 24;
			for (var y = 0; y < scaledHeight; y += 2)
				overlay[y, 0] = black;

			var overlaySheet = new Sheet(new Size(1, Exts.NextPowerOf2(scaledHeight)), false);
			overlaySheet.Texture.SetData(overlay);
			overlaySprite = new Sprite(overlaySheet, new Rectangle(0, 0, 1, scaledHeight), TextureChannel.Alpha);
		}

		public override void Draw()
		{
			if (video == null)
				return;

			if (!stopped && !paused)
			{
				var nextFrame = (int)float2.Lerp(0, video.Frames, Sound.VideoSeekPosition * invLength);
				if (nextFrame > video.Frames)
				{
					Stop();
					return;
				}

				while (nextFrame > video.CurrentFrame)
				{
					video.AdvanceFrame();
					if (nextFrame == video.CurrentFrame)
						videoSprite.sheet.Texture.SetData(video.FrameData);
				}
			}

			Game.Renderer.RgbaSpriteRenderer.DrawSprite(videoSprite, videoOrigin, videoSize);

			if (DrawOverlay)
				Game.Renderer.RgbaSpriteRenderer.DrawSprite(overlaySprite, videoOrigin, videoSize);
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (Hotkey.FromKeyInput(e) != CancelKey || e.Event != KeyInputEvent.Down)
				return false;

			Stop();
			return true;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			return RenderBounds.Contains(mi.Location);
		}

		public void Play()
		{
			PlayThen(() => { });
		}

		public void PlayThen(Action after)
		{
			if (video == null)
				return;

			onComplete = after;
			if (stopped)
				Sound.PlayVideo(video.AudioData);
			else
				Sound.PlayVideo();

			stopped = paused = false;
		}

		public void Pause()
		{
			if (stopped || paused || video == null)
				return;

			paused = true;
			Sound.PauseVideo();
		}

		public void Stop()
		{
			if (stopped || video == null)
				return;

			stopped = true;
			paused = true;
			Sound.StopVideo();
			video.Reset();
			videoSprite.sheet.Texture.SetData(video.FrameData);
			world.AddFrameEndTask(_ => onComplete());
		}
	}
}
