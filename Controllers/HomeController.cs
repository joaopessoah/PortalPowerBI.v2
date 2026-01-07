using Microsoft.AspNetCore.Mvc;
using PortalPowerBI.Services;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Mail;
using System.Linq;

namespace PortalPowerBI.Controllers
{
    public class HomeController : Controller
    {
        private readonly PbiService _pbiService;

        public HomeController(PbiService pbiService)
        {
            _pbiService = pbiService;
        }

        // ------------------------------------------------------------------
        // AÇÕES DE AUTENTICAÇÃO 
        // ------------------------------------------------------------------

        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("User") != null)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(string usuario, string senha)
        {
            var user = MockDb.Autenticar(usuario, senha);

            if (user != null && user.Ativo)
            {
                // CORREÇÃO ESSENCIAL para ArgumentNullException:
                // Usa user.Login como chave primária, mas se for nulo (pode ocorrer se autenticar por Email),
                // usa user.Email. Garante que a chave de sessão NUNCA é nula.
                string loginKey = user.Login ?? user.Email ?? usuario; 
                
                if (string.IsNullOrEmpty(loginKey))
                {
                    ViewBag.Erro = "Erro interno: Falha ao obter a chave de sessão.";
                    return View();
                }

                HttpContext.Session.SetString("User", loginKey);
                return RedirectToAction("Index");
            }

            ViewBag.Erro = "Usuário ou senha inválidos, ou conta inativa.";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public IActionResult TrocarSenha()
        {
            var login = HttpContext.Session.GetString("User");
            if (login == null) return RedirectToAction("Login");

            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            if (user == null) 
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }
            
            if (!user.PrecisaTrocarSenha) return RedirectToAction("Index");

            return View();
        }

        [HttpPost]
        public IActionResult SalvarNovaSenha(string novaSenha, string confirmarSenha)
        {
            var login = HttpContext.Session.GetString("User");
            if (login == null) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 4)
            {
                ViewBag.Erro = "A senha deve ter pelo menos 4 caracteres.";
                return View("TrocarSenha");
            }

            if (novaSenha != confirmarSenha)
            {
                ViewBag.Erro = "As senhas não coincidem.";
                return View("TrocarSenha");
            }

            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            if (user != null)
            {
                user.Senha = novaSenha;
                user.PrecisaTrocarSenha = false;
                MockDb.Salvar();

                HttpContext.Session.Clear(); 
                TempData["Sucesso"] = "Senha alterada com sucesso! Entre com a nova senha.";
                
                return RedirectToAction("Login");
            }

            return RedirectToAction("Login");
        }

        // ------------------------------------------------------------------
        // AÇÕES DE CONTEÚDO (Index e Visualizar)
        // ------------------------------------------------------------------

        public IActionResult Index()
        {
            var login = HttpContext.Session.GetString("User");
            if (login == null) return RedirectToAction("Login");
            
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login); 
            
            if (user == null) 
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }
            
            if (user.PrecisaTrocarSenha) return RedirectToAction("TrocarSenha");

            var idsPermitidos = user.Acessos.Select(a => a.ReportId).ToList();

            var conteudosPermitidos = MockDb.TodosApps 
                .Where(c => c.Id != null && idsPermitidos.Contains(c.Id))
                .ToList();

            ViewBag.NomeUsuario = user.Nome;
            return View(conteudosPermitidos); 
        }

        public async Task<IActionResult> Visualizar(string id)
        {
            var login = HttpContext.Session.GetString("User");
            if (login == null) return RedirectToAction("Login");
            
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            if (user == null) 
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            if (user.PrecisaTrocarSenha) return RedirectToAction("TrocarSenha");

            var acessoConfig = user.Acessos.FirstOrDefault(a => a.ReportId == id);
            if (acessoConfig == null) return Content("Acesso Negado.");

            var conteudoInfo = MockDb.TodosApps.FirstOrDefault(c => c.Id == id); 
            if (conteudoInfo == null || conteudoInfo.ReportEmbedId == null) return Content("Conteúdo não encontrado ou não configurado.");

            try 
            {
                string? usuarioRls = user.Email; 
                string? roleRls = acessoConfig.Role;
                
                if (string.IsNullOrEmpty(roleRls))
                {
                    usuarioRls = null;
                    roleRls = null;
                }

                var embedConfig = await _pbiService.GetEmbedTokenAsync(
                    conteudoInfo.ReportEmbedId, 
                    conteudoInfo.GroupId, 
                    usuarioRls, 
                    roleRls
                );

                return View(embedConfig);
            }
            catch (Exception ex) { return Content($"Erro: {ex.Message}"); }
        }
    }
}