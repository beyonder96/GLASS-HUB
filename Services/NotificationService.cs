using System;
using System.Collections.Generic;
using System.Linq;
using GlassHub.Models;

namespace GlassHub.Services
{
    public class NotificationService
    {
        public event Action? OnChange;
        public List<NotificationItem> Notifications { get; private set; } = new();

        public void AddNotification(string title, string description, NotificationType type)
        {
            // Avoid duplicates for auto-generated notifications
            if (Notifications.Any(n => n.Title == title && n.Description == description && !n.IsRead))
                return;

            Notifications.Insert(0, new NotificationItem 
            { 
                Title = title, 
                Description = description, 
                Timestamp = DateTime.Now,
                Type = type
            });
            NotifyStateChanged();
        }

        public void GenerateNotificationsFromInvoices(IEnumerable<Invoice> invoices)
        {
            // Clear old system notifications (optional, deciding strategy)
            // For now, let's keep it simple: clear all and regenerate based on current state.
            // In a real app we might want to persist manual notifications.
            Notifications.Clear();
            
            var today = DateTime.Today;
            
            // 1. Check for OVERDUE (Atrasado)
            var overdueInstallments = invoices
                .SelectMany(inv => inv.Installments.Select(inst => new { Invoice = inv, Installment = inst }))
                .Where(x => x.Installment.Status == PaymentStatus.OVERDUE || (x.Installment.Status == PaymentStatus.PENDING && x.Installment.DueDate < today));

            foreach (var item in overdueInstallments)
            {
                // We add them at the beginning, so we iterate and add.
                // Reversing or just adding is fine.
                AddNotification(
                    "Conta Atrasada", 
                    $"{item.Invoice.IssuerName} - R$ {item.Installment.Value:N2} venceu em {item.Installment.DueDate:dd/MM}.", 
                    NotificationType.Error
                );
            }

            // 2. Check for DUE TODAY (Hoje)
            var dueToday = invoices
                .SelectMany(inv => inv.Installments.Select(inst => new { Invoice = inv, Installment = inst }))
                .Where(x => x.Installment.Status == PaymentStatus.PENDING && x.Installment.DueDate == today);

            foreach (var item in dueToday)
            {
                AddNotification(
                    "Vence Hoje", 
                    $"{item.Invoice.IssuerName} - R$ {item.Installment.Value:N2} vence hoje.", 
                    NotificationType.Warning
                );
            }

            // 3. Check for DUE TOMORROW (Amanhã)
            var dueTomorrow = invoices
                .SelectMany(inv => inv.Installments.Select(inst => new { Invoice = inv, Installment = inst }))
                .Where(x => x.Installment.Status == PaymentStatus.PENDING && x.Installment.DueDate == today.AddDays(1));

            foreach (var item in dueTomorrow)
            {
                AddNotification(
                    "Vence Amanhã", 
                    $"{item.Invoice.IssuerName} - R$ {item.Installment.Value:N2} vence amanhã.", 
                    NotificationType.Info
                );
            }
            
            // 4. Paid recently? (Needs history, skipping for now)

            NotifyStateChanged();
        }

        public void MarkAllAsRead()
        {
            foreach(var n in Notifications) n.IsRead = true;
            NotifyStateChanged();
        }
        
        private void NotifyStateChanged() => OnChange?.Invoke();
    }

    public class NotificationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; } = false;
        public NotificationType Type { get; set; }
        
        public string TimeAgo 
        {
            get 
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalMinutes < 1) return "Agora";
                if (diff.TotalMinutes < 60) return $"Há {(int)diff.TotalMinutes} min";
                if (diff.TotalHours < 24) return $"Há {(int)diff.TotalHours} h";
                return $"Há {(int)diff.TotalDays} d";
            }
        }

        public string GetIconSvg()
        {
            return Type switch
            {
                NotificationType.Info => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M12 16v-4\"/><path d=\"M12 8h.01\"/></svg>",
                NotificationType.Success => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M12 2v20\"/><path d=\"M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6\"/></svg>",
                NotificationType.Warning => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M12 2v20\"/><path d=\"m17 5-5-3-5 3\"/><path d=\"m17 19-5 3-5-3\"/></svg>",
                NotificationType.Error => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z\"/><path d=\"M12 9v4\"/><path d=\"M12 17h.01\"/></svg>",
                _ => ""
            };
        }

        public string GetBgColorClass()
        {
             return Type switch
            {
                NotificationType.Info => "bg-blue-100 dark:bg-blue-500/10",
                NotificationType.Success => "bg-emerald-100 dark:bg-emerald-500/10",
                NotificationType.Warning => "bg-amber-100 dark:bg-amber-500/10",
                NotificationType.Error => "bg-rose-100 dark:bg-rose-500/10",
                _ => "bg-slate-100"
            };
        }
        
        public string GetTextColorClass()
        {
             return Type switch
            {
                NotificationType.Info => "text-blue-600 dark:text-blue-400",
                NotificationType.Success => "text-emerald-600 dark:text-emerald-400",
                NotificationType.Warning => "text-amber-600 dark:text-amber-400",
                NotificationType.Error => "text-rose-600 dark:text-rose-400",
                _ => "text-slate-600"
            };
        }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
