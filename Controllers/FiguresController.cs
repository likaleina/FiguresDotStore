using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FiguresDotStore.Controllers
{
	public static class FiguresStorage
	{
		public static bool CheckIfAvailable(string type, int count)
		{
			return RedisClient.Get(type) >= count;
		}

		public static void Reserve(string type, int count)
		{
			var current = RedisClient.Get(type);

			RedisClient.Set(type, current - count);
		}
	}

	public class Position
	{
		public string Type;

		public float SideA;
		public float SideB;
		public float SideC;

		public int Count;
	}

	public class Cart
	{
		public List<Position> positions;
	}

	public class Order
	{
		public List<Position> triangles;
		public List<Position> circles;
		public List<Position> squares;
	}

	[ApiController]
	[Route("[controller]")]
	public class FiguresController : ControllerBase
	{
		private readonly ILogger<FiguresController> _logger;

		public FiguresController(ILogger<FiguresController> logger)
		{
			_logger = logger;
		}

		[HttpPost]
		public ActionResult Order(Cart cart)
		{
			foreach (var position in cart.positions)
			{
				if (!FiguresStorage.CheckIfAvailable(position.Type, position.Count))
				{
					return new BadRequestResult();
				}
			}

			var order = new Order();

			order.circles = cart.positions.Where(c => c.Type == "Circle").ToList();
			order.triangles = cart.positions.Where(c => c.Type == "Triangle").ToList();
			order.squares = cart.positions.Where(c => c.Type == "Square").ToList();

			foreach (var position in cart.positions)
			{
				FiguresStorage.Reserve(position.Type, position.Count);
			}

			OrdersStorage.SaveOrder(order);

			return new OkResult();
		}
	}
}
