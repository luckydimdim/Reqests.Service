using System;
using System.Collections.Generic;
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

                if (contractId == null)
                {
                    var result = new List<SimpleRequestDto>();

                    var requests = await _requestsBusinessLayer.GetRequests();

                    foreach (var request in requests)
                    {
                        var simpleRequest = _autoMapper.Map<SimpleRequestDto>(request);

                        var contract = await _contractBusinessLayer.GetContract(simpleRequest.ContractId);
                        simpleRequest.ContractNumber = contract.Number;
                        simpleRequest.ContractorName = contract.ContractorName;
                        result.Add(simpleRequest);
                    }

                    return result;
                }
                else
                {
                    return await _requestsBusinessLayer.GetRequestsByContractId(contractId);
                }
            });

            /// <summary>
            /// /requests/{id} - получить заявку по указанному ID
            /// </summary>
            Get("/{id}", async args => await _requestsBusinessLayer.GetRequest(args.id));

            /// <summary>
            /// Создать заявку
            /// </summary>
            Post("/", async (args, ct) =>
            {
                var createRequestDto = this.Bind<CreateRequestDto>();

                string requestId = await _requestsBusinessLayer.CreateRequest(createRequestDto.ContractId, createRequestDto.CallOffOrderIds);

                CmasRequests.Request request = await _requestsBusinessLayer.GetRequest(requestId);

                DetailedRequestDto result = _autoMapper.Map<DetailedRequestDto>(request);

                result.Documents = await CreateTimeSheets(request.CallOffOrderIds);

                result.Summary.WorksQuantity = request.CallOffOrderIds.Count;

                return result;
            });

            Put("/{id}",  (args, ct) =>
            {
                var callOffsIds = this.Bind<List<string>>();

                throw new NotImplementedException("TODO: обновление заявки не реализовано");
            });

            /// <summary>
            /// Удалить заявку
            /// </summary>
            Delete("/{id}", async args =>
            {
                return await _requestsBusinessLayer.DeleteRequest(args.id);
            });
        }


    }
}
