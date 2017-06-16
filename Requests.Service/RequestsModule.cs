using System;
using System.Collections.Generic;
using Nancy.Extensions;
using Nancy;
using Nancy.ModelBinding;
using Cmas.Services.Requests.Dtos;
using System.Threading.Tasks;
using Cmas.BusinessLayers.Requests.Entities;
using Nancy.IO;
using Cmas.Infrastructure.ErrorHandler;
using System.Threading;
using Nancy.Responses.Negotiation;
using Nancy.Validation;
using Cmas.Infrastructure.Security;


namespace Cmas.Services.Requests
{
    public class RequestsModule : NancyModule
    {
        private IServiceProvider _serviceProvider;

        private RequestsService requestsService;

        private RequestsService _requestsService
        {
            get
            {
                if (requestsService == null)
                    requestsService = new RequestsService(_serviceProvider, Context);

                return requestsService;
            }
        }

        public RequestsModule(IServiceProvider serviceProvider) : base("/requests")
        {

            //this.RequiresAnyRole(new[] { Role.Contractor, Role.Customer });
            this.RequiresAuthentication();
            _serviceProvider = serviceProvider;
             
            /// <summary>
            /// /requests/ - получить список всех заявок
            /// </summary>
            Get<IEnumerable<SimpleRequestDto>>("/", GetRequestsHandlerAsync,
                (ctx) => !ctx.Request.Query.ContainsKey("contractId"));

            /// <summary>
            /// /requests?contractId={id} - получить заявки по указанному договору
            /// </summary>
            Get<IEnumerable<SimpleRequestDto>>("/", GetRequestsByContractHandlerAsync,
                (ctx) => ctx.Request.Query.ContainsKey("contractId"));

            /// <summary>
            /// /requests/{id} - получить заявку по указанному ID
            /// </summary>
            Get<DetailedRequestDto>("/{id}", GetRequestHandlerAsync);

            /// <summary>
            /// Создать заявку
            /// </summary>
            Post<DetailedRequestDto>("/", CreateRequestHandlerAsync);

            /// <summary>
            /// Обновить заявку
            /// На входе массив идентификаторов наряд заказов
            /// </summary>
            Put<DetailedRequestDto>("/{id}", UpdateRequestHandlerAsync);

            /// <summary>
            /// Обновить статус заявки
            /// </summary>
            Put<Negotiator>("{id}/status", UpdateRequestStatusHandlerAsync);

            /// <summary>
            /// Удалить заявку
            /// </summary>
            Delete<Negotiator>("/{id}", DeleteRequestHandlerAsync);

            /// <summary>
            /// Проверить сумму по договору
            /// </summary>
            Get<CheckAvailableAmountResponse[]>("{requestId}/check-amount", CheckAmountHandlerAsync);

        }

        #region Обработчики

        private async Task<IEnumerable<SimpleRequestDto>> GetRequestsHandlerAsync(dynamic args, CancellationToken ct)
        {
            return await _requestsService.GetRequestsAsync();
        }

        private async Task<CheckAvailableAmountResponse[]> CheckAmountHandlerAsync(dynamic args, CancellationToken ct)
        {
            return await _requestsService.CheckAmountAsync((string)args.requestId);
        }

        private async Task<IEnumerable<SimpleRequestDto>> GetRequestsByContractHandlerAsync(dynamic args,
            CancellationToken ct)
        {
            return await _requestsService.GetRequestsByContractAsync(Request.Query["contractId"]);
        }

        private async Task<DetailedRequestDto> GetRequestHandlerAsync(dynamic args, CancellationToken ct)
        {
            return await _requestsService.GetRequestAsync(args.id);
        }

        private async Task<DetailedRequestDto> CreateRequestHandlerAsync(dynamic args, CancellationToken ct)
        { 
            var request = this.Bind<CreateRequestDto>();

            var validationResult = this.Validate(request);

            if (!validationResult.IsValid)
            {
                throw new ValidationErrorException(validationResult.FormattedErrors);
            }

            return await _requestsService.CreateRequestAsync(request);
        }

        private async Task<DetailedRequestDto> UpdateRequestHandlerAsync(dynamic args, CancellationToken ct)
        {
            var ids = this.Bind<List<string>>();

            return await _requestsService.UpdateRequestAsync(args.id, ids);
        }

        private async Task<Negotiator> UpdateRequestStatusHandlerAsync(dynamic args, CancellationToken ct)
        {
            string statusSysName = (Request.Body as RequestStream).AsString();

            RequestStatus parsedStatus = RequestStatus.None;
             
            if (!Enum.TryParse<RequestStatus>(statusSysName, ignoreCase: true, result: out parsedStatus))
                throw new Exception("Incorrect status");

            await _requestsService.UpdateRequestStatusAsync(args.id, parsedStatus);

            return Negotiate.WithStatusCode(HttpStatusCode.OK);
        }

        private async Task<Negotiator> DeleteRequestHandlerAsync(dynamic args, CancellationToken ct)
        {
            await _requestsService.DeleteRequestAsync(args.id);

            return Negotiate.WithStatusCode(HttpStatusCode.OK);
        }

        #endregion
    }
}