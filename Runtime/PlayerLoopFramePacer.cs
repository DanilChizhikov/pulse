using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Frame pacer that yields through the player loop, without a <see cref="MonoBehaviour"/> driver.
	/// </summary>
	/// <remarks>
	/// The pacer inserts its own <see cref="PlayerLoopSystem"/> into the <c>Update</c> phase on the first wait
	/// request and restores the original loop on <see cref="Dispose"/> or when the application quits.
	/// Create it on the main thread: <see cref="IsFrameOverloaded"/> reads <see cref="Time.unscaledDeltaTime"/>
	/// and reports <c>false</c> when queried from any other thread.
	/// </remarks>
	[Preserve]
	public sealed class PlayerLoopFramePacer : IInitializationFramePacer, IDisposable
	{
		private const float DefaultMaxFrameSeconds = 0.1f;

		private readonly float _maxFrameSeconds;
		private readonly int _mainThreadId;
		private readonly object _gate = new();
		private readonly List<TaskCompletionSource<bool>> _pendingWaits = new();

		private PlayerLoopSystem _originalPlayerLoop;
		private bool _isHooked;
		private bool _isDisposed;

		/// <inheritdoc />
		public bool IsFrameOverloaded
		{
			get
			{
				if (_isDisposed || Thread.CurrentThread.ManagedThreadId != _mainThreadId)
				{
					return false;
				}

				return Time.unscaledDeltaTime > _maxFrameSeconds;
			}
		}
		
		/// <summary>
		/// Creates a pacer that treats frames longer than 100 ms as overloaded.
		/// </summary>
		public PlayerLoopFramePacer() : this(DefaultMaxFrameSeconds) { }

		/// <summary>
		/// Creates a pacer with a custom frame budget.
		/// </summary>
		/// <param name="maxFrameSeconds">Frame duration, in seconds, above which the next system is postponed.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the budget is not positive.</exception>
		public PlayerLoopFramePacer(float maxFrameSeconds)
		{
			if (maxFrameSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(maxFrameSeconds), "Frame budget must be positive.");
			}

			_maxFrameSeconds = maxFrameSeconds;
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		/// <inheritdoc />
		public Task WaitNextFrameAsync(CancellationToken token)
		{
			if (_isDisposed || token.IsCancellationRequested || !Application.isPlaying)
			{
				return Task.CompletedTask;
			}

			var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			lock (_gate)
			{
				if (!TryHook())
				{
					return Task.CompletedTask;
				}

				_pendingWaits.Add(completionSource);
			}

			if (!token.CanBeCanceled)
			{
				return completionSource.Task;
			}

			CancellationTokenRegistration registration = token.Register(
				state => ((TaskCompletionSource<bool>)state).TrySetResult(false),
				completionSource,
				useSynchronizationContext: false);
			completionSource.Task.ContinueWith(
				(_, state) => ((CancellationTokenRegistration)state).Dispose(),
				registration,
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);

			return completionSource.Task;
		}

		/// <summary>
		/// Restores the original player loop and releases every pending wait.
		/// </summary>
		public void Dispose()
		{
			TaskCompletionSource<bool>[] pendingWaits;
			lock (_gate)
			{
				if (_isDisposed)
				{
					return;
				}

				_isDisposed = true;
				Unhook();
				pendingWaits = _pendingWaits.ToArray();
				_pendingWaits.Clear();
			}

			for (int i = 0; i < pendingWaits.Length; i++)
			{
				pendingWaits[i].TrySetResult(false);
			}
		}
		
		private static bool TryAppendSubSystem(ref PlayerLoopSystem root, Type parentType, PlayerLoopSystem system)
		{
			PlayerLoopSystem[] subSystems = root.subSystemList;
			if (subSystems == null)
			{
				return false;
			}

			var copy = new PlayerLoopSystem[subSystems.Length];
			Array.Copy(subSystems, copy, subSystems.Length);

			for (int i = 0; i < copy.Length; i++)
			{
				if (copy[i].type == parentType)
				{
					PlayerLoopSystem[] children = copy[i].subSystemList;
					int childrenCount = children?.Length ?? 0;
					var newChildren = new PlayerLoopSystem[childrenCount + 1];
					if (childrenCount > 0)
					{
						Array.Copy(children, newChildren, childrenCount);
					}

					newChildren[childrenCount] = system;
					copy[i].subSystemList = newChildren;
					root.subSystemList = copy;
					return true;
				}

				PlayerLoopSystem child = copy[i];
				if (!TryAppendSubSystem(ref child, parentType, system))
				{
					continue;
				}

				copy[i] = child;
				root.subSystemList = copy;
				return true;
			}

			return false;
		}

		private void TickFrame()
		{
			TaskCompletionSource<bool>[] completedWaits;
			lock (_gate)
			{
				if (_pendingWaits.Count == 0)
				{
					return;
				}

				completedWaits = _pendingWaits.ToArray();
				_pendingWaits.Clear();
			}

			for (int i = 0; i < completedWaits.Length; i++)
			{
				completedWaits[i].TrySetResult(true);
			}
		}

		private bool TryHook()
		{
			if (_isHooked)
			{
				return true;
			}

			PlayerLoopSystem originalPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
			PlayerLoopSystem modifiedPlayerLoop = originalPlayerLoop;
			var pacerSystem = new PlayerLoopSystem
			{
				type = typeof(PlayerLoopFramePacer),
				updateDelegate = TickFrame,
			};

			if (!TryAppendSubSystem(ref modifiedPlayerLoop, typeof(UnityEngine.PlayerLoop.Update), pacerSystem))
			{
				return false;
			}

			_originalPlayerLoop = originalPlayerLoop;
			PlayerLoop.SetPlayerLoop(modifiedPlayerLoop);
			_isHooked = true;
			Application.quitting += ApplicationQuittingHandler;
			return true;
		}

		private void Unhook()
		{
			if (!_isHooked)
			{
				return;
			}

			_isHooked = false;
			Application.quitting -= ApplicationQuittingHandler;
			PlayerLoop.SetPlayerLoop(_originalPlayerLoop);
		}

		private void ApplicationQuittingHandler()
		{
			Dispose();
		}
	}
}
