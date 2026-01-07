using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace PortalPowerBI.Services
{
    // Define os tipos de conteúdo
    public static class ContentTypes
    {
        public const string App = "App";
        public const string Report = "Report";
    }

    public class AcessoRelatorio
    {
        public string ReportId { get; set; } = "";
        public string? Role { get; set; }
    }

    public class Usuario
    {
        public string? Login { get; set; }
        public string? Email { get; set; }
        public string? Senha { get; set; }
        public string? Nome { get; set; }
        public bool IsAdmin { get; set; }
        public bool Ativo { get; set; } = true;
        public bool PrecisaTrocarSenha { get; set; } = false;
        public List<AcessoRelatorio> Acessos { get; set; } = new List<AcessoRelatorio>();
    }

    // CLASSE UNIFICADA: Representa Apps ou Reports
    public class AppInfo
    {
        public string? Id { get; set; } // ID Principal (Report ID ou App ID)
        public string? GroupId { get; set; } // Workspace ID (opcional para Apps)
        public string? Nome { get; set; }
        public string? Descricao { get; set; }
        public bool RequerRls { get; set; } = false;
        public string? ContentType { get; set; } = ContentTypes.Report;
        public string? ReportEmbedId { get; set; } // ID do Report de destino
    }

    public static class MockDb
    {
        private static string _fileUsuarios = "usuarios.json";
        private static string _fileConteudo = "apps.json";

        public static List<AppInfo> TodosApps { get; set; } = new(); 
        public static List<Usuario> Usuarios { get; set; } = new();

        static MockDb()
        {
            Carregar();
        }

        private static void Carregar()
        {
            if (File.Exists(_fileConteudo))
            {
                try {
                    string json = File.ReadAllText(_fileConteudo);
                    TodosApps = JsonSerializer.Deserialize<List<AppInfo>>(json) ?? new();
                } catch { TodosApps = new List<AppInfo>(); }
            }

            if (File.Exists(_fileUsuarios))
            {
                try {
                    string json = File.ReadAllText(_fileUsuarios);
                    Usuarios = JsonSerializer.Deserialize<List<Usuario>>(json) ?? new();
                } catch { Usuarios = new List<Usuario>(); }
            }

            // CORREÇÃO CRUCIAL: Garante que o usuário admin exista com LOGIN e EMAIL preenchidos.
            if (!Usuarios.Any() || !Usuarios.Any(u => u.Login == "admin"))
            {
                Usuarios.Add(new Usuario { 
                    Login = "admin", 
                    Email = "admin@portal.com", 
                    Senha = "123", 
                    Nome = "Super Admin", 
                    IsAdmin = true, 
                    Ativo = true 
                });
            }
        }

        public static void Salvar()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_fileConteudo, JsonSerializer.Serialize(TodosApps, options));
            File.WriteAllText(_fileUsuarios, JsonSerializer.Serialize(Usuarios, options));
        }

        public static Usuario? Autenticar(string loginOuEmail, string senha)
        {
            if (string.IsNullOrEmpty(loginOuEmail)) return null;
            string entrada = loginOuEmail.ToLower().Trim();

            return Usuarios.FirstOrDefault(u => 
                ((u.Login != null && u.Login.ToLower() == entrada) || 
                 (u.Email != null && u.Email.ToLower() == entrada)) 
                && u.Senha == senha
            );
        }
    }
}