﻿using Microsoft.AspNetCore.Mvc;

namespace AngryMonkey.Cloud.Login.Controllers
{
	public class BaseController : Controller
	{
		public static CloudLoginConfiguration? Configuration;

		private CosmosMethods? _cosmosMethods;
		internal CosmosMethods CosmosMethods
		{
			get
			{
				return Configuration?.Cosmos == null
					? throw new Exception()
					: (_cosmosMethods ??= new CosmosMethods(Configuration.Cosmos.ConnectionString, Configuration.Cosmos.DatabaseId, Configuration.Cosmos.RequestContainerId, Configuration.Cosmos.ContainerId));
			}
		}
	}
}
