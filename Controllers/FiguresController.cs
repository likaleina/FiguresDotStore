using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FiguresDotStore.Controllers
{
	public class OrderResult
	{

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
		public ActionResult<OrderResult> Order()
		{
			return new EmptyResult();
		}
	}
}
