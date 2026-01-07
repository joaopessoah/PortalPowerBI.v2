using Microsoft.AspNetCore.Mvc;
using PortalPowerBI.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace PortalPowerBI.Controllers
{
    public class AdminController : Controller
    {
        private bool IsAdmin()
        {
            var login = HttpContext.Session.GetString("User");
            if (login == null) return false;
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            return user != null && user.IsAdmin && user.Ativo;
        }

        public IActionResult Index() => IsAdmin() ? View() : RedirectToAction("Index", "Home");

        // --- MÉTODOS DE USUÁRIOS ---
        public IActionResult Usuarios() => IsAdmin() ? View(MockDb.Usuarios) : RedirectToAction("Index", "Home");

        [HttpPost]
        public IActionResult CriarUsuario(string login, string email, string senha, string nome, bool isAdmin)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            if (MockDb.Usuarios.Any(u => u.Login == login)) return RedirectToAction("Usuarios");

            var novoUser = new Usuario
            {
                Login = login, Email = email, Senha = senha, Nome = nome,
                IsAdmin = isAdmin, Ativo = true, PrecisaTrocarSenha = true,
                Acessos = ProcessarAcessosDoFormulario(Request.Form)
            };

            MockDb.Usuarios.Add(novoUser);
            MockDb.Salvar();
            return RedirectToAction("Usuarios");
        }

        public IActionResult EditarUsuario(string login)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            return user == null ? RedirectToAction("Usuarios") : View(user);
        }

        [HttpPost]
        public IActionResult SalvarEdicao(string loginOriginal, string login, string email, string senha, string nome, bool isAdmin, bool ativo, bool resetarSenha)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == loginOriginal);
            if (user != null)
            {
                user.Login = login; 
                user.Email = email;
                user.Senha = senha;
                user.Nome = nome;
                user.IsAdmin = isAdmin;
                user.Ativo = ativo;
                user.PrecisaTrocarSenha = resetarSenha;
                user.Acessos = ProcessarAcessosDoFormulario(Request.Form);
                MockDb.Salvar();
            }
            return RedirectToAction("Usuarios");
        }

        private List<AcessoRelatorio> ProcessarAcessosDoFormulario(IFormCollection form)
        {
            var listaAcessos = new List<AcessoRelatorio>();
            var idsMarcados = form["relatoriosSelecionados"].ToList();

            foreach (var idReport in idsMarcados)
            {
                string roleName = form[$"role_{idReport}"];
                listaAcessos.Add(new AcessoRelatorio 
                { 
                    ReportId = idReport,
                    Role = string.IsNullOrWhiteSpace(roleName) ? null : roleName.Trim()
                });
            }
            return listaAcessos;
        }

        public IActionResult AlternarStatus(string login)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            if (user != null)
            {
                user.Ativo = !user.Ativo;
                MockDb.Salvar();
            }
            return RedirectToAction("Usuarios");
        }

        public IActionResult ExcluirUsuario(string login)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var user = MockDb.Usuarios.FirstOrDefault(u => u.Login == login);
            if (user != null && user.Login != "admin")
            {
                MockDb.Usuarios.Remove(user);
                MockDb.Salvar();
            }
            return RedirectToAction("Usuarios");
        }


        // --- CRIAÇÃO E EDIÇÃO DE CONTEÚDO (APPS/REPORTS) ---
        public IActionResult Relatorios() => IsAdmin() ? View(MockDb.TodosApps) : RedirectToAction("Index", "Home");

        [HttpPost]
        public IActionResult CriarRelatorio(string id, string groupId, string nome, string descricao, bool requerRls, string reportEmbedId, string type)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            
            if (type == ContentTypes.Report)
            {
                reportEmbedId = id; 
            }
            else if (type == ContentTypes.App)
            {
                groupId = "00000000-0000-0000-0000-000000000000"; 
            }

            MockDb.TodosApps.Add(new AppInfo {
                Id = id, 
                GroupId = groupId, 
                Nome = nome, 
                Descricao = descricao, 
                RequerRls = requerRls, 
                ReportEmbedId = reportEmbedId, 
                ContentType = type 
            });
            MockDb.Salvar();
            return RedirectToAction("Relatorios");
        }

        public IActionResult EditarRelatorio(string id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            // Se o App ID (id) não for encontrado, appInfo será null e a Action retorna
            var appInfo = MockDb.TodosApps.FirstOrDefault(r => r.Id == id); 
            
            // Se appInfo for nulo, redireciona de volta. Isso é o que causa a sensação de "não fazer nada".
            return appInfo == null ? RedirectToAction("Relatorios") : View(appInfo); 
        }

        [HttpPost]
        public IActionResult SalvarEdicaoRelatorio(string idOriginal, string idNovo, string groupId, string nome, string descricao, bool requerRls, string reportEmbedId, string contentType)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var appInfo = MockDb.TodosApps.FirstOrDefault(r => r.Id == idOriginal);
            if (appInfo != null)
            {
                if (idOriginal != idNovo)
                {
                    foreach (var user in MockDb.Usuarios)
                    {
                        var acesso = user.Acessos.FirstOrDefault(a => a.ReportId == idOriginal);
                        if (acesso != null) acesso.ReportId = idNovo;
                    }
                }
                
                appInfo.ReportEmbedId = string.IsNullOrEmpty(reportEmbedId) ? idNovo : reportEmbedId; 
                appInfo.Id = idNovo; 
                appInfo.GroupId = groupId; 
                appInfo.Nome = nome; 
                appInfo.Descricao = descricao;
                appInfo.RequerRls = requerRls;
                appInfo.ContentType = contentType; 
                
                MockDb.Salvar();
            }
            return RedirectToAction("Relatorios");
        }

public IActionResult ExcluirRelatorio(string id)
{
    if (!IsAdmin()) return RedirectToAction("Index", "Home");
    var appInfo = MockDb.TodosApps.FirstOrDefault(r => r.Id == id);
    if (appInfo != null)
    {
        // 1. Remove o item
        MockDb.TodosApps.Remove(appInfo); 
        
        // 2. Remove o acesso do item a todos os usuários
        foreach (var user in MockDb.Usuarios)
        {
            user.Acessos.RemoveAll(a => a.ReportId == id);
        }
        
        // 3. Salva a mudança no disco
        MockDb.Salvar(); 
    }
    // 4. Redireciona para atualizar a lista
    return RedirectToAction("Relatorios");
}
    }
}