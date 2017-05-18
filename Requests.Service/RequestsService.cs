using AutoMapper;
using Cmas.Infrastructure.Domain.Commands;
using Cmas.Infrastructure.Domain.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Cmas.BusinessLayers.Requests;
using Cmas.BusinessLayers.CallOffOrders;
using Cmas.BusinessLayers.TimeSheets;
using Cmas.BusinessLayers.Contracts;
using Cmas.BusinessLayers.Requests.Entities;
using Microsoft.Extensions.Logging;
using Cmas.Services.Requests.Dtos;
using Cmas.Infrastructure.ErrorHandler;
using Cmas.BusinessLayers.TimeSheets.Entities;
using Cmas.BusinessLayers.CallOffOrders.Entities;
using Nancy;
using Request = Cmas.BusinessLayers.Requests.Entities.Request;

namespace Cmas.Services.Requests
{
    public class RequestsService
    {
        private readonly RequestsBusinessLayer _requestsBusinessLayer;
        private readonly CallOffOrdersBusinessLayer _callOffOrdersBusinessLayer;
        private readonly ContractsBusinessLayer _contractsBusinessLayer;
        private readonly TimeSheetsBusinessLayer _timeSheetsBusinessLayer;
        private readonly IMapper _autoMapper;
        private readonly NancyContext _context;
        private ILogger _logger;

        public RequestsService(IServiceProvider serviceProvider, NancyContext ctx)
        {
            _context = ctx;
            var _commandBuilder = (ICommandBuilder) serviceProvider.GetService(typeof(ICommandBuilder));
            var _queryBuilder = (IQueryBuilder) serviceProvider.GetService(typeof(IQueryBuilder));
            var _loggerFactory = (ILoggerFactory) serviceProvider.GetService(typeof(ILoggerFactory));

            _autoMapper = (IMapper) serviceProvider.GetService(typeof(IMapper));
            _logger = _loggerFactory.CreateLogger<RequestsService>();

            _callOffOrdersBusinessLayer = new CallOffOrdersBusinessLayer(serviceProvider, ctx.CurrentUser);
            _contractsBusinessLayer = new ContractsBusinessLayer(serviceProvider, ctx.CurrentUser);
            _timeSheetsBusinessLayer = new TimeSheetsBusinessLayer(serviceProvider, ctx.CurrentUser);
            _requestsBusinessLayer = new RequestsBusinessLayer(serviceProvider, ctx.CurrentUser);
        }

        public async Task<string> DeleteRequestAsync(string requestId)
        {
            // удаляем табели
            IEnumerable<TimeSheet> timeSheets = await _timeSheetsBusinessLayer.GetTimeSheetsByRequestId(requestId);

            _logger.LogInformation(string.Format("deleting time-sheets before request: {0} ...",
                string.Join(",", timeSheets.Select(t => t.Id))));

            foreach (var timeSheet in timeSheets)
            {
                await _timeSheetsBusinessLayer.DeleteTimeSheet(timeSheet.Id);
            }

            _logger.LogInformation("Deleting request...");

            return await _requestsBusinessLayer.DeleteRequest(requestId);
        }

        /// <summary>
        /// Получить название статуса.
        /// TODO: Перенести в класс - локализатор
        /// </summary>
        private string GetRequestStatusName(RequestStatus status)
        {
            switch (status)
            {
                case RequestStatus.Empty:
                    return "Не заполнена";
                case RequestStatus.Creating:
                    return "Заполнение";
                case RequestStatus.Created:
                    return "Заполнена";
                case RequestStatus.Approving:
                    return "На проверке";
                case RequestStatus.Correcting:
                    return "Содержит ошибки";
                case RequestStatus.Corrected:
                    return "Исправлена";
                case RequestStatus.Approved:
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


                var timeSheetDto = _autoMapper.Map<TimeSheetDto>(timeSheet);
                timeSheetDto.Assignee = callOffOrder.Assignee;
                timeSheetDto.Name = callOffOrder.Name;
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

            _logger.LogInformation(string.Format("creating time sheets for request with id = {0} call off orders: {1}",
                requestId, string.Join(",", callOffOrderIds)));

            foreach (var callOffOrderId in callOffOrderIds)
            {
                _logger.LogInformation(string.Format("step 1. call off order = {0}", callOffOrderId));

                CallOffOrder callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);
                IEnumerable<TimeSheet> timeSheets =
                    await _timeSheetsBusinessLayer.GetTimeSheetsByCallOffOrderId(callOffOrderId);

                if (!callOffOrder.StartDate.HasValue || !callOffOrder.FinishDate.HasValue)
                {
                    _logger.LogWarning(string.Format("callOffOrder {0} has incorrect period"));
                    continue;
                }

                DateTime startDate = callOffOrder.StartDate.Value;

                // FIXME: Изменить после преобразования из string в DateTime
                DateTime finishDate = callOffOrder.FinishDate.Value;


                _logger.LogInformation(string.Format("step 2. startDate = {0} finishDate = {1}", startDate.ToString(),
                    finishDate.ToString()));

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
                        _logger.LogInformation(string.Format("time sheet found within {0}.{1}. Skipping this month..",
                            startDate.Month, startDate.Year));
                        startDate = startDate.AddMonths(1);
                        continue;
                    }
                    else
                    {
                        _logger.LogInformation(string.Format("creating time sheet for period {0}.{1}", startDate.Month,
                            startDate.Year));

                        timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                            startDate.Month, startDate.Year, requestId, callOffOrder.CurrencySysName);
                        created = true;
                        break;
                    }
                }

                if (!created)
                {
                    _logger.LogInformation(string.Format("creating time sheet for period {0}.{1}", finishDate.Month,
                        finishDate.Year));
                    timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                        finishDate.Month, finishDate.Year, requestId, callOffOrder.CurrencySysName);
                }

                createdTimeSheets.Add(timeSheetId);
            }

            _logger.LogInformation(string.Format("created time sheets: {0}", string.Join(",", createdTimeSheets)));

            return createdTimeSheets;
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(string requestId)
        {
            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            return await GetDetailedRequest(request);
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(Request request)
        {
            DetailedRequestDto result = _autoMapper.Map<DetailedRequestDto>(request);

            var contract = await _contractsBusinessLayer.GetContract(request.ContractId);

            result.Documents = await GetTimeSheets(request.CallOffOrderIds, request.Id);

            result.Summary.WorksQuantity = request.CallOffOrderIds.Count;

            var requestCurrencies = result.Documents.Select(d => d.CurrencySysName).Distinct();

            foreach (var currency in requestCurrencies)
            {
                var worksAmount = new AmountDto
                {
                    CurrencySysName = currency,
                    Value = result.Documents.Where(doc => doc.CurrencySysName == currency).Sum(doc => doc.Amount)
                };

                result.Summary.Totals.Add(worksAmount);
                result.Summary.Amounts.Add(worksAmount);
                result.Summary.WorksAmount.Add(worksAmount);

                if (contract.VatIncluded)
                {
                    var vat = new AmountDto
                    {
                        CurrencySysName = currency,
                        Value = Math.Round((worksAmount.Value / 1.18 - worksAmount.Value) * -1)
                    };

                    result.Summary.Vats.Add(vat);
                }
            }


            result.StatusName = GetRequestStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<SimpleRequestDto> GetSimpleRequest(Request request)
        {
            var result = _autoMapper.Map<SimpleRequestDto>(request);

            var documents = await GetTimeSheets(request.CallOffOrderIds, request.Id);

            var contract = await _contractsBusinessLayer.GetContract(result.ContractId);
            result.ContractNumber = contract.Number;
            result.ContractorName = contract.ContractorName;

            result.Amount = documents.Sum(doc => doc.Amount);

            result.StatusName = GetRequestStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<IEnumerable<SimpleRequestDto>> GetSimpleRequests(IEnumerable<Request> requests)
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

        /// <summary>
        /// Обновление статуса заявки. Одновременно, если надо, меняется статус первичной документации
        /// </summary>
        public async Task UpdateRequestStatusAsync(string requestId, RequestStatus status)
        {
            _logger.LogInformation(string.Format("changing request status. request = {0} status = {1}", requestId,
                status.ToString()));

            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            var timeSheets = await _timeSheetsBusinessLayer.GetTimeSheetsByRequestId(requestId);

            if (timeSheets.Where(t => t.Status == TimeSheetStatus.Empty || t.Status == TimeSheetStatus.Creating).Any())
            {
                throw new GeneralServiceErrorException(
                    string.Format("Can not change status from {0} to {1}", request.Status, status));
            }

            await _requestsBusinessLayer.UpdateRequestStatusAsync(request, status);

            //TODO: переделать на событийную модель (шину)
            if (status == RequestStatus.Approving)
            {
                foreach (var timeSheet in timeSheets.Where(t => t.Status != TimeSheetStatus.Approved))
                {
                    await _timeSheetsBusinessLayer.UpdateTimeSheetStatus(timeSheet, TimeSheetStatus.Approving);
                }
            }
        }

        public async Task<DetailedRequestDto> UpdateRequestAsync(string requestId, IList<string> callOffOrderIds)
        {
            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            request.CallOffOrderIds = callOffOrderIds;

            await _requestsBusinessLayer.UpdateRequest(request);

            return await GetDetailedRequest(request);
        }

        public async Task<DetailedRequestDto> CreateRequestAsync(CreateRequestDto request)
        {
            string requestId = null;
            IEnumerable<string> createdTimeSheetIds = null;
            try
            {
                requestId = await _requestsBusinessLayer.CreateRequest(request.ContractId,
                    request.CallOffOrderIds);

                createdTimeSheetIds = await CreateTimeSheetsAsync(requestId, request.CallOffOrderIds);
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
        }

        public async Task<DetailedRequestDto> GetRequestAsync(string requestId)
        {
            return await GetDetailedRequest(requestId);
        }

        public async Task<IEnumerable<SimpleRequestDto>> GetRequestsAsync()
        {
            var result = await _requestsBusinessLayer.GetRequests();

            return await GetSimpleRequests(result);
        }

        public async Task<IEnumerable<SimpleRequestDto>> GetRequestsByContractAsync(string contractId)
        {
            var result = await _requestsBusinessLayer.GetRequestsByContractId(contractId);

            return await GetSimpleRequests(result);
        }
    }
}