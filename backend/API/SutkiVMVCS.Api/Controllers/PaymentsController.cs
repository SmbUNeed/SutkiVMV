using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Services.Shared;
using SutkiVMVCS.Services.YooKassa;
using System.Text.Json;
using Telegram.Bot.Requests.Abstractions;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMaxService _max;
    private readonly IYooKassaService _yooKassa;

    public PaymentsController(AppDbContext context, IMaxService max, IYooKassaService yooKassa)
    {
        _context = context;
        _max = max;
        _yooKassa = yooKassa;
    }

    
}