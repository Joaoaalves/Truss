using Truss.Cli.Templates;

namespace Truss.Cli
{
    /// <summary>
    /// Installs the support module: a Support context scaffolded into the
    /// user's projects, in the account slice's mold. The Ticket aggregate,
    /// its slices, persistence and tests are the user's code once written;
    /// the routes land on the Program markers like every module's.
    /// </summary>
    internal static class SupportScaffolder
    {
        public static int Install(TrussManifest manifest, string root, string? deckUrl, Action<string> log)
        {
            if (!manifest.Modules.Contains("auth"))
            {
                log("Tickets belong to signed-in users, so the auth module comes first. Run: truss add auth");
                return 1;
            }

            if (Directory.Exists(Path.Combine(root, manifest.ApplicationProject, "Support")))
            {
                log("A Support context already exists; refusing to overwrite it.");
                return 1;
            }

            if (deckUrl is not null)
                return InstallDeckMode(manifest, root, deckUrl, log);

            if (!manifest.UsesEntityFramework)
            {
                log("Standalone support stores tickets in the database and requires one. Scaffold with --database, or point at a deck with --deck <url>.");
                return 1;
            }

            var (idType, idNamespace) = AuthScaffolder.InstalledAccountIdentity(manifest, root);

            string Render(string template)
            {
                return CodeGenerator.DedupeUsings(template
                    .Replace("__NS_USERID__", idNamespace)
                    .Replace("__USERID__", idType)
                    .Replace("__NAME__", manifest.Name));
            }

            void Write(string relativePath, string template)
            {
                var path = Path.Combine(root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Render(template) + Environment.NewLine);
            }

            WriteDomain(manifest, Write);
            WriteApplication(manifest, Write);
            WriteInfrastructure(manifest, Write);

            if (manifest.Modules.Contains("jobs"))
            {
                Write(
                    Path.Combine(manifest.ApplicationProject, "Support", "CloseExpiredTickets", "CloseExpiredTicketsJob.cs"),
                    SupportTemplates.CloseExpiredTicketsJob);
            }

            WireProgram(manifest, root, Render, log);
            WireWorker(manifest, root, log);

            if (manifest.Tests)
            {
                Write(Path.Combine(manifest.DomainTestsProject, "Support", "TicketTests.cs"), SupportTemplates.DomainTests);
                Write(Path.Combine(manifest.IntegrationTestsProject, "Support", "SupportTests.cs"), SupportTemplates.IntegrationTests);
            }

            manifest.Settings["support.mode"] = "standalone";

            log("The Support context was scaffolded into your projects. It is your code: edit the Ticket, the rules and the routes freely.");
            log("Customers use /support/tickets; staff answers through /support/queue. The reopen window lives in SupportPolicy.cs.");

            if (!manifest.Modules.Contains("rbac"))
                log("The staff routes are protected by authentication only. Install rbac to protect them with the support.manage permission.");

            if (!manifest.Modules.Contains("jobs"))
                log("Without the jobs module, resolved tickets close only when staff closes them. Install jobs to get the hourly auto-close sweep.");

            return 0;
        }


        /// <summary>
        /// The centralized mode: no local domain, only the thin surface. The
        /// same four customer routes as standalone, backed by the deck's
        /// ingestion API through the typed client; attendance happens on the
        /// deck, so no staff routes exist here.
        /// </summary>
        private static int InstallDeckMode(TrussManifest manifest, string root, string deckUrl, Action<string> log)
        {
            if (!Uri.TryCreate(deckUrl, UriKind.Absolute, out _))
            {
                log($"'{deckUrl}' is not an absolute url. Pass the deck's base address: --deck https://deck.example.com");
                return 1;
            }

            string Render(string template) => CodeGenerator.DedupeUsings(template.Replace("__NAME__", manifest.Name));

            void Write(string relativePath, string template)
            {
                var path = Path.Combine(root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Render(template) + Environment.NewLine);
            }

            var support = Path.Combine(manifest.ApplicationProject, "Support");

            Write(Path.Combine(support, "ISupportRequesterSource.cs"), SupportTemplates.DeckRequesterSource);
            Write(Path.Combine(support, "AccountRequesterSource.cs"), SupportTemplates.DeckAccountRequesterSource);
            Write(Path.Combine(support, "OpenTicket", "OpenTicket.cs"), SupportTemplates.DeckOpenTicket);
            Write(Path.Combine(support, "OpenTicket", "OpenTicketHandler.cs"), SupportTemplates.DeckOpenTicketHandler);
            Write(Path.Combine(support, "OpenTicket", "OpenTicketValidator.cs"), SupportTemplates.DeckOpenTicketValidator);
            Write(Path.Combine(support, "ReplyToMyTicket", "ReplyToMyTicket.cs"), SupportTemplates.DeckReply);
            Write(Path.Combine(support, "ReplyToMyTicket", "ReplyToMyTicketHandler.cs"), SupportTemplates.DeckReplyHandler);
            Write(Path.Combine(support, "ReplyToMyTicket", "ReplyToMyTicketValidator.cs"), SupportTemplates.DeckReplyValidator);
            Write(Path.Combine(support, "ListMyTickets", "ListMyTickets.cs"), SupportTemplates.DeckList);
            Write(Path.Combine(support, "ListMyTickets", "ListMyTicketsHandler.cs"), SupportTemplates.DeckListHandler);
            Write(Path.Combine(support, "ListMyTickets", "ListMyTicketsValidator.cs"), SupportTemplates.DeckListValidator);
            Write(Path.Combine(support, "GetMyTicket", "GetMyTicket.cs"), SupportTemplates.DeckGet);
            Write(Path.Combine(support, "GetMyTicket", "GetMyTicketHandler.cs"), SupportTemplates.DeckGetHandler);
            Write(Path.Combine(support, "SupportNotificationHandler.cs"), manifest.Modules.Contains("email")
                ? SupportTemplates.DeckNotificationHandlerEmail
                : SupportTemplates.DeckNotificationHandlerLog);

            CsprojEditor.AddPackageReference(Path.Combine(root, manifest.ApplicationProject, Path.GetFileName(manifest.ApplicationProject) + ".csproj"), "Truss.Support", manifest.TrussVersion);
            CsprojEditor.AddPackageReference(Path.Combine(root, manifest.ApiProject, Path.GetFileName(manifest.ApiProject) + ".csproj"), "Truss.Support", manifest.TrussVersion);

            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");

            SourceEditor.InsertAfter(program, $"using {manifest.Name}.Application;", Render(SupportTemplates.DeckProgramUsings));

            if (!SourceEditor.InsertAtMarker(program, Markers.Services, Render(SupportTemplates.DeckProgramServices)))
                log("Could not update Program.cs automatically. Register: AddTrussSupportDeck and the ISupportRequesterSource.");

            var endpoints = Render(SupportTemplates.DeckProgramEndpoints)
                + Environment.NewLine
                + Render(SupportTemplates.DeckWebhookEndpoint);

            if (!SourceEditor.InsertAtMarker(program, Markers.Endpoints, endpoints))
                log("Could not update Program.cs automatically. Map the support routes before app.Run().");

            AppSettingsEditor.SetSection(root, manifest, "Truss:Support:Deck", new Dictionary<string, object>
            {
                ["Url"] = deckUrl
            }, log);

            manifest.Settings["support.mode"] = "deck";
            manifest.Settings["support.deck.url"] = deckUrl;

            WireWorkerDeck(manifest, root, log);

            log($"The support surface now speaks to the deck at {deckUrl}. Register this application there and set the key per environment: Truss__Support__Deck__ApiKey.");
            log("Attendance happens on the deck; this application keeps only the customer routes. If the deck is unreachable, the support routes degrade and the rest of the application does not care.");

            return 0;
        }

        /// <summary>
        /// The worker validates the same handlers at boot, so the deck client
        /// and the requester source must be registered there too, in whichever
        /// order the worker and support arrive.
        /// </summary>
        internal static void WireWorkerDeck(TrussManifest manifest, string root, Action<string> log)
        {
            var workerProject = Path.Combine("src", $"{manifest.Name}.Worker");
            var program = Path.Combine(root, workerProject, "Program.cs");

            if (!File.Exists(program) || File.ReadAllText(program).Contains("AddTrussSupportDeck", StringComparison.Ordinal))
                return;

            SourceEditor.InsertAfter(
                program,
                $"using {manifest.Name}.Application;",
                $"using {manifest.Name}.Application.Support;");

            var services = "builder.Services.AddTrussSupportDeck(options => builder.Configuration.GetSection(\"Truss:Support:Deck\").Bind(options));"
                + Environment.NewLine
                + "builder.Services.AddScoped<ISupportRequesterSource, AccountRequesterSource>();";

            if (!SourceEditor.InsertAtMarker(program, Markers.Services, services)
                && !SourceEditor.InsertBefore(program, "builder.Build().Run();", services))
            {
                log("Could not update the worker's Program.cs automatically. Register the deck client and the requester source there too.");
                return;
            }

            CsprojEditor.AddPackageReference(
                Path.Combine(root, workerProject, $"{manifest.Name}.Worker.csproj"), "Truss.Support", manifest.TrussVersion);

            AppSettingsEditor.SetSection(
                Path.Combine(root, workerProject, "appsettings.json"),
                "Truss:Support:Deck",
                new Dictionary<string, object> { ["Url"] = manifest.Settings.GetValueOrDefault("support.deck.url", string.Empty) },
                log);

            log("The worker's Program.cs was updated too.");
        }

        private static void WriteDomain(TrussManifest manifest, Action<string, string> write)
        {
            var support = Path.Combine(manifest.DomainProject, "Support");
            var ticket = Path.Combine(support, "Ticket");
            var valueObjects = Path.Combine(ticket, "ValueObjects");
            var rules = Path.Combine(ticket, "Rules");

            write(Path.Combine(support, "SupportPolicy.cs"), SupportTemplates.SupportPolicy);
            write(Path.Combine(ticket, "Ticket.cs"), SupportTemplates.Ticket);
            write(Path.Combine(ticket, "TicketMessage.cs"), SupportTemplates.TicketMessage);
            write(Path.Combine(ticket, "TicketStatus.cs"), SupportTemplates.TicketStatus);
            write(Path.Combine(ticket, "TicketPriority.cs"), SupportTemplates.TicketPriority);
            write(Path.Combine(ticket, "MessageVisibility.cs"), SupportTemplates.MessageVisibility);
            write(Path.Combine(ticket, "MessageAuthorKind.cs"), SupportTemplates.MessageAuthorKind);
            write(Path.Combine(ticket, "Events", "TicketEvents.cs"), SupportTemplates.Events);
            write(Path.Combine(valueObjects, "TicketId.cs"), SupportTemplates.TicketId);
            write(Path.Combine(valueObjects, "TicketMessageId.cs"), SupportTemplates.TicketMessageId);
            write(Path.Combine(valueObjects, "TicketSubject.cs"), SupportTemplates.TicketSubject);
            write(Path.Combine(valueObjects, "MessageBody.cs"), SupportTemplates.MessageBody);
            write(Path.Combine(rules, "TicketSubjectMustFitLength.cs"), SupportTemplates.RuleSubjectLength);
            write(Path.Combine(rules, "MessageBodyMustFitLength.cs"), SupportTemplates.RuleBodyLength);
            write(Path.Combine(rules, "TicketMustExist.cs"), SupportTemplates.RuleTicketMustExist);
            write(Path.Combine(rules, "TicketMustNotBeClosed.cs"), SupportTemplates.RuleNotClosed);
            write(Path.Combine(rules, "TicketMustBeActive.cs"), SupportTemplates.RuleMustBeActive);
            write(Path.Combine(rules, "TicketMustAcceptCustomerReplies.cs"), SupportTemplates.RuleAcceptsCustomerReply);
            write(Path.Combine(rules, "ResolvedTicketTakesOnlyInternalNotes.cs"), SupportTemplates.RuleResolvedTakesOnlyNotes);
        }

        private static void WriteApplication(TrussManifest manifest, Action<string, string> write)
        {
            var support = Path.Combine(manifest.ApplicationProject, "Support");

            write(Path.Combine(support, "ITicketRepository.cs"), SupportTemplates.Repository);
            write(Path.Combine(support, "DTOs", "TicketDtos.cs"), SupportTemplates.Dtos);
            write(Path.Combine(support, "OpenTicket", "OpenTicket.cs"), SupportTemplates.OpenTicket);
            write(Path.Combine(support, "OpenTicket", "OpenTicketHandler.cs"), SupportTemplates.OpenTicketHandler);
            write(Path.Combine(support, "OpenTicket", "OpenTicketValidator.cs"), SupportTemplates.OpenTicketValidator);
            write(Path.Combine(support, "ReplyToMyTicket", "ReplyToMyTicket.cs"), SupportTemplates.ReplyToMyTicket);
            write(Path.Combine(support, "ReplyToMyTicket", "ReplyToMyTicketHandler.cs"), SupportTemplates.ReplyToMyTicketHandler);
            write(Path.Combine(support, "ReplyToMyTicket", "ReplyToMyTicketValidator.cs"), SupportTemplates.ReplyToMyTicketValidator);
            write(Path.Combine(support, "ListMyTickets", "ListMyTickets.cs"), SupportTemplates.ListMyTickets);
            write(Path.Combine(support, "ListMyTickets", "ListMyTicketsHandler.cs"), SupportTemplates.ListMyTicketsHandler);
            write(Path.Combine(support, "ListMyTickets", "ListMyTicketsValidator.cs"), SupportTemplates.ListMyTicketsValidator);
            write(Path.Combine(support, "GetMyTicket", "GetMyTicket.cs"), SupportTemplates.GetMyTicket);
            write(Path.Combine(support, "GetMyTicket", "GetMyTicketHandler.cs"), SupportTemplates.GetMyTicketHandler);
            write(Path.Combine(support, "ListSupportQueue", "ListSupportQueue.cs"), SupportTemplates.ListSupportQueue);
            write(Path.Combine(support, "ListSupportQueue", "ListSupportQueueHandler.cs"), SupportTemplates.ListSupportQueueHandler);
            write(Path.Combine(support, "ListSupportQueue", "ListSupportQueueValidator.cs"), SupportTemplates.ListSupportQueueValidator);
            write(Path.Combine(support, "GetTicketForStaff", "GetTicketForStaff.cs"), SupportTemplates.GetTicketForStaff);
            write(Path.Combine(support, "GetTicketForStaff", "GetTicketForStaffHandler.cs"), SupportTemplates.GetTicketForStaffHandler);
            write(Path.Combine(support, "ReplyAsStaff", "ReplyAsStaff.cs"), SupportTemplates.ReplyAsStaff);
            write(Path.Combine(support, "ReplyAsStaff", "ReplyAsStaffHandler.cs"), SupportTemplates.ReplyAsStaffHandler);
            write(Path.Combine(support, "ReplyAsStaff", "ReplyAsStaffValidator.cs"), SupportTemplates.ReplyAsStaffValidator);
            write(Path.Combine(support, "ResolveTicket", "ResolveTicket.cs"), SupportTemplates.ResolveTicket);
            write(Path.Combine(support, "ResolveTicket", "ResolveTicketHandler.cs"), SupportTemplates.ResolveTicketHandler);
            write(Path.Combine(support, "CloseTicket", "CloseTicket.cs"), SupportTemplates.CloseTicket);
            write(Path.Combine(support, "CloseTicket", "CloseTicketHandler.cs"), SupportTemplates.CloseTicketHandler);
            write(Path.Combine(support, "SetTicketPriority", "SetTicketPriority.cs"), SupportTemplates.SetTicketPriority);
            write(Path.Combine(support, "SetTicketPriority", "SetTicketPriorityHandler.cs"), SupportTemplates.SetTicketPriorityHandler);
        }

        private static void WriteInfrastructure(TrussManifest manifest, Action<string, string> write)
        {
            var support = Path.Combine(manifest.InfrastructureProject, "Support");

            write(Path.Combine(support, "TicketConfiguration.cs"), SupportTemplates.Configuration);
            write(Path.Combine(support, "EfTicketRepository.cs"), SupportTemplates.EfRepository);
            write(Path.Combine(manifest.InfrastructureProject, "SupportModule.cs"), SupportTemplates.SupportModule);
        }

        private static void WireProgram(TrussManifest manifest, string root, Func<string, string> render, Action<string> log)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");

            SourceEditor.InsertAfter(program, $"using {manifest.Name}.Application;", render(SupportTemplates.ProgramUsings));

            var services = render(SupportTemplates.ProgramServices);

            if (manifest.Modules.Contains("jobs"))
                services += Environment.NewLine + render(SupportTemplates.ProgramRecurringJob);

            if (!SourceEditor.InsertAtMarker(program, Markers.Services, services))
                log($"Could not update Program.cs automatically. Add before building the app: {services}");

            // Staff routes carry a permission when rbac is installed; with
            // authentication alone they still demand a signed-in caller.
            var staff = manifest.Modules.Contains("rbac")
                ? ".RequirePermission(\"support.manage\")"
                : ".RequireAuthorization()";

            var endpoints = render(SupportTemplates.ProgramEndpoints).Replace("__STAFF__", staff);

            if (!SourceEditor.InsertAtMarker(program, Markers.Endpoints, endpoints))
            {
                log("Could not update Program.cs automatically. Add before app.Run():");
                log(endpoints);
            }

            if (manifest.Modules.Contains("rbac"))
            {
                var role = "    options.AddRole(\"support\", \"support.manage\");";

                if (!File.ReadAllText(program).Contains("AddRole(\"support\"", StringComparison.Ordinal)
                    && !SourceEditor.InsertAfter(program, "options.AddRole(\"admin\", \"admin.access\");", role.Trim()))
                {
                    log("Could not add the support role automatically. Add inside AddTrussRbac: options.AddRole(\"support\", \"support.manage\");");
                }
            }
        }

        /// <summary>
        /// The worker validates the same handlers at boot, so the ticket
        /// repository must be registered there too, in whichever order the
        /// worker and support arrive. The worker-first order is handled here;
        /// the worker-second order by the worker's registration harvest.
        /// </summary>
        private static void WireWorker(TrussManifest manifest, string root, Action<string> log)
        {
            var program = Path.Combine(root, "src", $"{manifest.Name}.Worker", "Program.cs");

            if (!File.Exists(program) || File.ReadAllText(program).Contains("AddSupportInfrastructure", StringComparison.Ordinal))
                return;

            var registration = "builder.Services.AddSupportInfrastructure();";

            if (!SourceEditor.InsertAtMarker(program, Markers.Services, registration)
                && !SourceEditor.InsertBefore(program, "builder.Build().Run();", registration))
            {
                log($"Could not update the worker's Program.cs automatically. Add before builder.Build().Run(): {registration}");
                return;
            }

            log("The worker's Program.cs was updated too.");
        }
    }
}
