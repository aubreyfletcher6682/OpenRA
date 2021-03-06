﻿#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections;

namespace OpenRA.Primitives
{
	public interface IObservableCollection
	{
		event Action<object> OnAdd;
		event Action<object> OnRemove;
		event Action<int> OnRemoveAt;
		event Action<object, object> OnSet;
		event Action OnRefresh;
		IEnumerable ObservedItems { get; }
	}
}
