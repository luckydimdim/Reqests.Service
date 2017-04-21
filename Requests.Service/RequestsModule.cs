using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
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
using Cmas.Infrastructure.ErrorHandler;
using Microsoft.Extensions.Logging;
using Cmas.BusinessLayers.TimeSheets;
using Cmas.BusinessLayers.TimeSheets.Entities;
using Cmas.BusinessLayers.CallOffOrders.Entities;
using System.Linq;
using System.Globalization;

namespace Cmas.Services.Requests
{
    public class RequestsModule : NancyModule
    {
        private readonly ICommandBuilder _commandBuilder;
        private readonly IQueryBuilder _queryBuilder;
        private readonly RequestsBusinessLayer _requestsBusinessLayer;
        private readonly CallOffOrdersBusinessLayer _callOffOrdersBusinessLayer;
        private readonly ContractBusinessLayer _contractBusinessLayer;
        private readonly TimeSheetsBusinessLayer _timeSheetsBusinessLayer;
        private readonly IMapper _autoMapper;
        private ILogger _logger;

        /// <summary>
        /// Получить название статуса.
        /// TODO: Перенести в класс - локализатор
        /// </summary>
        private string GetRequestStatusName(RequestStatus status)
        {
            switch (status)
            {
                case RequestStatus.Creation:
                    return "Не заполнена";
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
        ///  
        /// </summary>
        private async Task<IEnumerable<TimeSheetDto>> GetTimeSheets(IEnumerable<string> callOffOrderIds,
            string requestId)
        {
            var result = new List<TimeSheetDto>();

            foreach (var callOffOrderId in callOffOrderIds)
            {
                var callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);

                TimeSheet timeSheet = null;
                try
                {
                    timeSheet =
                        await _timeSheetsBusinessLayer.GetTimeSheetByCallOffOrderAndRequest(callOffOrderId, requestId);
                }
                catch (NotFoundErrorException exc)
                {
                    _logger.Log(LogLevel.Warning, (EventId) 0,
                        String.Format("Time sheet by call-off order {0} and request {1} not found", callOffOrderId,
                            requestId), exc,
                        (state, error) => state.ToString());
                    continue;
                }


                var timeSheetDto = new TimeSheetDto();
                timeSheetDto.Id = timeSheet.Id;
                timeSheetDto.Assignee = callOffOrder.Assignee;
                timeSheetDto.CreatedAt = timeSheet.CreatedAt;
                timeSheetDto.UpdatedAt = timeSheet.CreatedAt;
                timeSheetDto.Name = callOffOrder.Name;
                timeSheetDto.Amount = timeSheet.Amount;
                timeSheetDto.Position = callOffOrder.Position;
                timeSheetDto.StatusName = TimeSheetsBusinessLayer.GetStatusName(timeSheet.Status);
                timeSheetDto.StatusSysName = timeSheet.Status.ToString();

                result.Add(timeSheetDto);
            }

            return result;
        }

        public async Task<IEnumerable<string>> CreateTimeSheetsAsync(string requestId,
            IEnumerable<string> callOffOrderIds)
        {
            var createdTimeSheets = new List<string>();

            foreach (var callOffOrderId in callOffOrderIds)
            {
                CallOffOrder callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);
                IEnumerable<TimeSheet> timeSheets =
                    await _timeSheetsBusinessLayer.GetTimeSheetsByCallOffOrderId(callOffOrderId);

                // FIXME: Изменить после преобразования из string в DateTime
                DateTime startDate = DateTime.ParseExact(callOffOrder.StartDate, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture);

                // FIXME: Изменить после преобразования из string в DateTime
                DateTime finishDate = DateTime.ParseExact(callOffOrder.FinishDate, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture);

                string timeSheetId = null;
                bool created = false;
                while (startDate < finishDate)
                {
                    var tsExist =
                        timeSheets.Where(
                                ts =>
                                    (ts.Month == startDate.Month &&
                                     ts.Year == startDate.Year))
                            .Any();

                    if (tsExist)
                    {
                        startDate = startDate.AddMonths(1);
                        continue;
                    }
                    else
                    {
                        timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                            startDate.Month, startDate.Year, requestId);
                        created = true;
                        break;
                    }
                }

                if (!created)
                {
                    timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                        finishDate.Month, finishDate.Year, requestId);
                }

                createdTimeSheets.Add(timeSheetId);
            }

            return createdTimeSheets;
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(string requestId)
        {
            CmasRequests.Request request = await _requestsBusinessLayer.GetRequest(requestId);

            return await GetDetailedRequest(request);
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(CmasRequests.Request request)
        {
            DetailedRequestDto result = _autoMapper.Map<DetailedRequestDto>(request);

            var contract = await _contractBusinessLayer.GetContract(request.ContractId);
             
            result.Documents = await GetTimeSheets(request.CallOffOrderIds, request.Id);

            result.Summary.WorksQuantity = request.CallOffOrderIds.Count;
            result.Summary.WorksAmount = result.Documents.Sum(doc => doc.Amount);
            result.Summary.Total = result.Summary.WorksAmount;
            result.Summary.Amount = result.Summary.WorksAmount;

            if (contract.VatIncluded)
            {
                result.Summary.Vat = Math.Round((result.Summary.WorksAmount / 1.18 - result.Summary.WorksAmount) * -1);
            }

            result.StatusName = GetRequestStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<SimpleRequestDto> GetSimpleRequest(CmasRequests.Request request)
        {
            var result = _autoMapper.Map<SimpleRequestDto>(request);


            var contract = await _contractBusinessLayer.GetContract(result.ContractId);
            result.ContractNumber = contract.Number;
            result.ContractorName = contract.ContractorName;

            result.StatusName = GetRequestStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<IEnumerable<SimpleRequestDto>> GetSimpleRequests(IEnumerable<CmasRequests.Request> requests)
        {
            var result = new List<SimpleRequestDto>();

            foreach (var request in requests)
            {
                SimpleRequestDto simpleRequest = null;
                try
                {
                    simpleRequest = await GetSimpleRequest(request);
                }
                catch (NotFoundErrorException exc)
                {
                    _logger.Log(LogLevel.Warning, (EventId) 0,
                        String.Format("Contract {0} not found", request.ContractId), exc,
                        (state, error) => state.ToString());
                    continue;
                }

                result.Add(simpleRequest);
            }

            return result;
        }

        public RequestsModule(ICommandBuilder commandBuilder, IQueryBuilder queryBuilder, IMapper autoMapper,
            ILoggerFactory loggerFactory) : base("/requests")
        {
            _autoMapper = autoMapper;
            _commandBuilder = commandBuilder;
            _queryBuilder = queryBuilder;
            _logger = loggerFactory.CreateLogger<RequestsModule>();

            _requestsBusinessLayer = new RequestsBusinessLayer(_commandBuilder, _queryBuilder);
            _callOffOrdersBusinessLayer = new CallOffOrdersBusinessLayer(_commandBuilder, _queryBuilder);
            _contractBusinessLayer = new ContractBusinessLayer(_commandBuilder, _queryBuilder);
            _timeSheetsBusinessLayer = new TimeSheetsBusinessLayer(_commandBuilder, _queryBuilder);

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
            Get("/{id}", async args => { return await GetDetailedRequest(args.id); });

            /// <summary>
            /// Создать заявку
            /// </summary>
            /// <return>DetailedRequestDto</return>
            Post("/", async (args, ct) =>
            {
                var createRequestDto = this.Bind<CreateRequestDto>();

                string requestId = null;
                IEnumerable<string> createdTimeSheetIds = null;
                try
                {
                    requestId = await _requestsBusinessLayer.CreateRequest(createRequestDto.ContractId,
                        createRequestDto.CallOffOrderIds);

                    createdTimeSheetIds = await CreateTimeSheetsAsync(requestId, createRequestDto.CallOffOrderIds);
                    return await GetDetailedRequest(requestId);
                }
                catch (Exception exc)
                {
                    if (!string.IsNullOrEmpty(requestId))
                    {
                        await _requestsBusinessLayer.DeleteRequest(requestId);

                        if (createdTimeSheetIds != null)
                        {
                            foreach (var timeSheetId in createdTimeSheetIds)
                            {
                                await _timeSheetsBusinessLayer.DeleteTimeSheet(timeSheetId);
                            }
                        }
                    }

                    throw exc;
                }
            });

            /// <summary>
            /// Обновить заявку
            /// На входе массив идентификаторов наряд заказов
            /// </summary>
            /// <return>DetailedRequestDto</return>
            Put("/{id}", async (args, ct) =>
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
                // удаляем табели

                IEnumerable<string> ids = await _timeSheetsBusinessLayer.GetTimeSheetsByRequestId(args.id);

                foreach (var id in ids)
                {
                    await _timeSheetsBusinessLayer.DeleteTimeSheet(id);
                }

                return await _requestsBusinessLayer.DeleteRequest(args.id);
            });
        }
    }
}