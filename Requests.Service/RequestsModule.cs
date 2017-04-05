using System;
using System.Collections.Generic;
using Nancy.Extensions;
using Cmas.BusinessLayers.CallOffOrders;
using Cmas.BusinessLayers.Requests;
using CmasRequests = Cmas.BusinessLayers.Requests.Entities;
using Cmas.Infrastructure.Domain.Commands;
using Cmas.Infrastructure.Domain.Queries;
using Nancy;
using Nancy.ModelBinding;
using Cmas.Services.Requests.Dtos;
using AutoMapper;
using System.Threading.Tasks;
using Cmas.BusinessLayers.Contracts;
using Cmas.BusinessLayers.Requests.Entities;
using Nancy.IO;

namespace Cmas.Services.Requests
{

    public class RequestsModule : NancyModule
    {
        private readonly ICommandBuilder _commandBuilder;
        private readonly IQueryBuilder _queryBuilder;
        private readonly RequestsBusinessLayer _requestsBusinessLayer;
        private readonly CallOffOrdersBusinessLayer _callOffOrdersBusinessLayer;
        private readonly ContractBusinessLayer _contractBusinessLayer;
        private readonly IMapper _autoMapper;

        /// <summary>
        /// Получить название статуса.
        /// TODO: Перенести в класс - локализатор
        /// </summary>
        private string GetStatusName(RequestStatus status)
        {
            switch (status)
            {
                case RequestStatus.Creation:
                    return "В процессе составления";
                case RequestStatus.Validation:
                    return "На проверке";
                case RequestStatus.Correction:
                    return "Содержит ошибки";
                case RequestStatus.Done:
                    return "Проверена";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Заглушка - создание TS на каждый НЗ.Удалить после реализации сервиса работ с TS
        /// </summary>
        private async Task<IEnumerable<TimeSheetDto>> CreateTimeSheets(IEnumerable<string> callOffOrderIds)
        {
            var result = new List<TimeSheetDto>();

            int i = 1;

            foreach (var callOffOrderId in callOffOrderIds)
            {
                var callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);

                var timeSheet = new TimeSheetDto();
                timeSheet.Id = callOffOrder.Id + "_" + i.ToString();
                timeSheet.Assignee = callOffOrder.Assignee;
                timeSheet.CreatedAt = DateTime.Now;
                timeSheet.UpdatedAt = DateTime.Now;
                timeSheet.Name = callOffOrder.Name;
                timeSheet.Position = callOffOrder.Position;

                i++;
                result.Add(timeSheet);
            }

            return result;
        }

        private async Task<DetailedRequestDto>  GetDetailedRequest(string requestId)
        {
            CmasRequests.Request request = await _requestsBusinessLayer.GetRequest(requestId);

            return await GetDetailedRequest(request);
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(CmasRequests.Request request)
        {
            DetailedRequestDto result = _autoMapper.Map<DetailedRequestDto>(request);

            result.Documents = await CreateTimeSheets(request.CallOffOrderIds);

            result.Summary.WorksQuantity = request.CallOffOrderIds.Count;

            result.StatusName = GetStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<SimpleRequestDto> GetSimpleRequest(CmasRequests.Request request)
        {
            var result = _autoMapper.Map<SimpleRequestDto>(request);

            var contract = await _contractBusinessLayer.GetContract(result.ContractId);
            result.ContractNumber = contract.Number;
            result.ContractorName = contract.ContractorName;

            result.StatusName = GetStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<IEnumerable<SimpleRequestDto>> GetSimpleRequests(IEnumerable<CmasRequests.Request> requests)
        {
            var result = new List<SimpleRequestDto>();

            foreach (var request in requests)
            {
                var simpleRequest = await GetSimpleRequest(request);
                result.Add(simpleRequest);
            }

            return result;
        }

        public RequestsModule(ICommandBuilder commandBuilder, IQueryBuilder queryBuilder, IMapper autoMapper) : base("/requests")
        {
            _autoMapper = autoMapper;
            _commandBuilder = commandBuilder;
            _queryBuilder = queryBuilder;

            _requestsBusinessLayer = new RequestsBusinessLayer(_commandBuilder, _queryBuilder);
            _callOffOrdersBusinessLayer = new CallOffOrdersBusinessLayer(_commandBuilder, _queryBuilder);
            _contractBusinessLayer = new ContractBusinessLayer(_commandBuilder, _queryBuilder);

            /// <summary>
            /// /requests/ - получить список всех заявок
            /// /requests?contractId={id} - получить заявки по указанному договору
            /// </summary>
            Get("/", async (args, ct) =>
            {
                string contractId = Request.Query["contractId"];

                IEnumerable<CmasRequests.Request> requests = null;

                if (contractId == null)
                {
                    requests = await _requestsBusinessLayer.GetRequests();
                }
                else
                {
                    requests = await _requestsBusinessLayer.GetRequestsByContractId(contractId);
                }

                return await GetSimpleRequests(requests);
            });

            /// <summary>
            /// /requests/{id} - получить заявку по указанному ID
            /// </summary>
            /// <return>DetailedRequestDto</return>
            Get("/{id}", async args =>
            {
                return await GetDetailedRequest(args.id);
            });

            /// <summary>
            /// Создать заявку
            /// </summary>
            /// <return>DetailedRequestDto</return>
            Post("/", async (args, ct) =>
            {
                var createRequestDto = this.Bind<CreateRequestDto>();

                string requestId = await _requestsBusinessLayer.CreateRequest(createRequestDto.ContractId, createRequestDto.CallOffOrderIds);

                return await GetDetailedRequest(requestId);
            });

            /// <summary>
            /// Обновить заявку
            /// На входе массив идентификаторов наряд заказов
            /// </summary>
            /// <return>DetailedRequestDto</return>
            Put("/{id}",  async (args, ct) =>
            {
                var ids = this.Bind<List<string>>();
                 
                CmasRequests.Request request = await _requestsBusinessLayer.GetRequest(args.id);

                request.CallOffOrderIds = ids;

                await _requestsBusinessLayer.UpdateRequest(request);

                return await GetDetailedRequest(request);
            });

            /// <summary>
            /// Обновить заявку
            /// На входе массив идентификаторов наряд заказов
            /// </summary>
            /// <return>DetailedRequestDto</return>
            Put("{id}/status", async (args, ct) =>
            {
                string statusSysName = (Request.Body as RequestStream).AsString();

                RequestStatus parsedStatus = RequestStatus.None;

                if (!Enum.TryParse<RequestStatus>(statusSysName, ignoreCase: true, result: out parsedStatus))
                    throw new Exception("Incorrect status");

                CmasRequests.Request request = await _requestsBusinessLayer.GetRequest(args.id);

                if (request.Status != parsedStatus)
                {
                    request.Status = parsedStatus;

                    await _requestsBusinessLayer.UpdateRequest(request);
                }
                
                return await GetDetailedRequest(request);
            });

            /// <summary>
            /// Удалить заявку
            /// </summary>
            /// <return>ID заявки</return>
            Delete("/{id}", async args =>
            {
                return await _requestsBusinessLayer.DeleteRequest(args.id);
            });
        }

    }
}
