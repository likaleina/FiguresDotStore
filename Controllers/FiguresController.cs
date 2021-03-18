using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

//везде добавить логирование
namespace FiguresDotStore.Controllers
{
	//объявить в отдельном проекте сервиса по работе с внешним api
	internal interface IRedisClient
	{
		int Get(string type);
		void Set(string type, int current);
	}
	//объявить в отдельном проекте сервиса по работе с внешним api (его нельзя сделать асинхронным?)
	//в случае, если RedisClient будет недоступен, все ляжет, предусмотреть появление ошибок, и логирование их
	public static class FiguresStorage
	{
		// корректно сконфигурированный и готовый к использованию клиент Редиса
		private static IRedisClient RedisClient { get; }
	
		public static bool CheckIfAvailable(string type, int count)
		{
			//например try-catch или проверять statusCode
			return RedisClient.Get(type) >= count;
		}

		public static void Reserve(string type, int count)
		{
			//например try-catch или проверять statusCode
			var current = RedisClient.Get(type);

			RedisClient.Set(type, current - count);
		}
	}
	//все классы моделей также вынести в отдельный проект, например Order.Models
	public class Position
	{
		public string Type { get; set; }

		public float SideA { get; set; }
		public float SideB { get; set; }
		public float SideC { get; set; }

		public int Count { get; set; }
	}
	//Order.Models
	public class Cart
	{
		//public Cart(} {
		//Positions = new List<Position>();
		//}
		//можно заменить List<Position> на ICollection<Position>
		public List<Position> Positions { get; set; }
	}
	//Order.Models
	public class Order
	{
		//public Order(} {
		//Positions = new List<Position>();
		//}
		//можно заменить List<Position> на ICollection<Position>
		//и название тоже мне кажется можно было бы заменить на FigurePositions
		public List<Figure> Positions { get; set; }
		//не нашла использования
		//не учтен квадрат
		public decimal GetTotal() =>
			Positions.Select(p => p switch
				{
					Triangle => (decimal) p.GetArea() * 1.2m,
					Circle => (decimal) p.GetArea() * 0.9m
				}) //если тут будет пусто, то вроде бы получим ошибку, надо проверить на null
				.Sum();
	}
	//Order.Models
	public abstract class Figure
	{
		public float SideA { get; set; }
		public float SideB { get; set; }
		public float SideC { get; set; }

		public abstract void Validate();
		public abstract double GetArea();
	}
	//Order.Models
	public class Triangle : Figure
	{
		public override void Validate()
		{
			bool CheckTriangleInequality(float a, float b, float c) => a < b + c;
			if (CheckTriangleInequality(SideA, SideB, SideC)
			    && CheckTriangleInequality(SideB, SideA, SideC)
			    && CheckTriangleInequality(SideC, SideB, SideA)) 
				return;
			throw new InvalidOperationException("Triangle restrictions not met");
		}

		public override double GetArea()
		{
			var p = (SideA + SideB + SideC) / 2;
			return Math.Sqrt(p * (p - SideA) * (p - SideB) * (p - SideC));
		}
		
	}
	//Order.Models
	public class Square : Figure
	{
		public override void Validate()
		{
			if (SideA < 0)
				throw new InvalidOperationException("Square restrictions not met");
			
			if (SideA != SideB)
				throw new InvalidOperationException("Square restrictions not met");
		}

		public override double GetArea() => SideA * SideA;
	}
	//Order.Models
	public class Circle : Figure
	{
		public override void Validate()
		{
			if (SideA < 0)
				throw new InvalidOperationException("Circle restrictions not met");
		}

		public override double GetArea() => Math.PI * SideA * SideA;
	}
	//реализацию и сервис объявить там же в отдельном проекте сервиса OrdersCreatorService
	//либо если это внешнее апи, то там же где и IRedisClient
	public interface IOrderStorage
	{
		// сохраняет оформленный заказ и возвращает сумму
		Task<decimal> Save(Order order);
	}
	
	[ApiController]
	[Route("[controller]")]
	public class FiguresController : ControllerBase
	{
		private readonly ILogger<FiguresController> _logger;
		private readonly IOrderStorage _orderStorage;
		//private readonly IOrdersCreatorService _orderCreatorService;

		public FiguresController(ILogger<FiguresController> logger, IOrderStorage orderStorage/*, IOrdersCreatorService orderCreatorService*/)
		{
			_logger = logger;
			_orderStorage = orderStorage;
			//_orderCreatorService = orderCreatorService;
		}

		// хотим оформить заказ и получить в ответе его стоимость
		[HttpPost]
		//Название не подходит, лучше CreateOrder
		//не вижу ни одного await... убрать async, либо все методы тоже сделать async-await
		public async Task<ActionResult> Order(Cart cart)
		{
		//try {
		//логику работы лучше вынести в отдельный класс в отдельном сервисе, например OrdersCreatorService : IOrdersCreatorService 
		//в Startup.ConfigureServices использовать services.AddTransient<IOrdersCreatorService, OrdersCreatorService>();
			foreach (var position in cart.Positions)
			{
				if (!FiguresStorage.CheckIfAvailable(position.Type, position.Count))
				{
					return new BadRequestResult();
				}
			}

			var order = new Order
			{
				Positions = cart.Positions.Select(p =>
				{
					Figure figure = p.Type switch
					{
						"Circle" => new Circle(),
						"Triangle" => new Triangle(),
						"Square" => new Square()
					};
					figure.SideA = p.SideA;
					figure.SideB = p.SideB;
					figure.SideC = p.SideC;
					//обрабатывать exception, чтобы до пользователя доходило нормальное сообщение, напр. try-catch, 
					//либо переделать Validate, чтобы он возвращал строку и можно было ее показывать пользоаателю в случае неуспеха
					figure.Validate();
					return figure;
				}).ToList()
			};

			foreach (var position in cart.Positions)
			{
			//обработку на случай неуспешного обращения, например вернуть пользователю сообщение
				FiguresStorage.Reserve(position.Type, position.Count);
			}
			//добавить проверку на случай, что не удалось провалидировать или зарезервировать, тогда сохранение не должно выполняться
			//вот тут явно await пропущен
			//так же нужна проверка на случай, если запрос отвалится в ходе выполнения
			var result = _orderStorage.Save(order);
		//все что внутри try в отдельный сервис, здесь вызов метода и возврат результата пользователю
			return new OkObjectResult(result.Result);
			//}
			//catch(Exception ex){
			//_logger.Log($"{ex.Message}\n{ex.StackTrace}";
			//ну и пользователю что-нить сказать надо, типа попробуйте еще раз
		}
	}
}
