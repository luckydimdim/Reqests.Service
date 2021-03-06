﻿using AutoMapper;
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
using Cmas.BusinessLayers.Contracts.Entities;
using Cmas.Infrastructure.Security;

namespace Cmas.Services.Requests
{
    /// <summary>
    /// Сервис заявок на проверку
    /// </summary>
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

        /// <summary>
        /// Удалить заявку
        /// </summary>
        public async Task<string> DeleteRequestAsync(string requestId)
        {
            var request = await _requestsBusinessLayer.GetRequest(requestId);
            bool isAdmin = _context.CurrentUser.HasRole(Role.Administrator);

            if (!isAdmin && request.Status == RequestStatus.Approved)
                throw new ForbiddenErrorException();

            // удаляем табели
            IEnumerable<string> timeSheetIds = await _timeSheetsBusinessLayer.GetTimeSheetsIdsByRequestId(requestId);

            _logger.LogInformation($"deleting time-sheets before request: {string.Join(",", timeSheetIds)} ...");
             
            foreach (var timeSheetId in timeSheetIds)
            {
                await _timeSheetsBusinessLayer.DeleteTimeSheet(timeSheetId);
            }
            
            _logger.LogInformation("Deleting request...");

            await _requestsBusinessLayer.DeleteRequest(requestId);

            _logger.LogInformation("request deleted");

            return requestId;
        }

        /// <summary>
        /// Обновление статуса заявки. 
        /// Одновременно, если надо, меняется статус табелей
        /// </summary>
        public async Task UpdateRequestStatusAsync(string requestId, RequestStatus status)
        {
            _logger.LogInformation($"changing request status. request = {requestId} status = {status}");

            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            if (request == null)
            {
                throw new NotFoundErrorException();
            }

            var timeSheets = await _timeSheetsBusinessLayer.GetTimeSheetsByRequestId(requestId);

            string timeSheetIds = string.Join(",", timeSheets.Select(t => t.Id));

            _logger.LogInformation($"requests's time-sheet ids: {timeSheetIds}");

            // нельзя менять статус заявки, если есть незаполненные табели
            if (timeSheets.Where(t => t.Status == TimeSheetStatus.Empty || t.Status == TimeSheetStatus.Creating).Any())
            {
                throw new GeneralServiceErrorException($"Can not change status from {request.Status} to {status}");
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

            _logger.LogInformation($"updating completed");
        }

        /// <summary>
        /// Обновление состава заявки (наряд заказов)
        /// </summary>
        public async Task<DetailedRequestDto> UpdateRequestAsync(string requestId, IList<string> callOffOrderIds)
        {
            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            if (request == null)
            {
                throw new NotFoundErrorException();
            }

            // если есть табели, их надо удалить
            foreach (var callOffOrderId in request.CallOffOrderIds)
            {
                var timeSheet =
                    await _timeSheetsBusinessLayer.GetTimeSheetByCallOffOrderAndRequest(callOffOrderId, requestId);

                if (timeSheet != null)
                {
                    _logger.LogInformation($"timesheet found: {timeSheet.Id} with status {timeSheet.Status}");

                    if (timeSheet.Status != TimeSheetStatus.Empty && timeSheet.Status != TimeSheetStatus.Creating)
                    {
                        throw new GeneralServiceErrorException(
                            $"cannot delete timesheet with status {timeSheet.Status}");
                    }
                    else
                    {
                        _logger.LogInformation($"deleting timesheet {timeSheet.Id}...");
                        await _timeSheetsBusinessLayer.DeleteTimeSheet(timeSheet.Id);
                        _logger.LogInformation("timesheet deleted");
                    }
                }
            }

            request.CallOffOrderIds = callOffOrderIds;

            _logger.LogInformation("recreating time sheets...");

            var createdTimeSheetIds = await CreateTimeSheetsAsync(requestId, request.CallOffOrderIds);

            _logger.LogInformation("updating request...");

            await _requestsBusinessLayer.UpdateRequest(request);

            _logger.LogInformation("done");

            return await GetDetailedRequest(request);
        }

        /// <summary>
        /// Создать заявку
        /// </summary>
        public async Task<DetailedRequestDto> CreateRequestAsync(CreateRequestDto request)
        {
            string requestId = null;
            IEnumerable<string> createdTimeSheetIds = null;

            try
            {
                requestId = await _requestsBusinessLayer.CreateRequest(request.ContractId, request.CallOffOrderIds);

                _logger.LogInformation($"request created with id {requestId}");

                createdTimeSheetIds = await CreateTimeSheetsAsync(requestId, request.CallOffOrderIds);

                return await GetDetailedRequest(requestId);
            }
            catch (Exception exc)
            {
                _logger.LogError("Error while request creating. Deleting request and time sheets");

                if (!string.IsNullOrEmpty(requestId))
                {
                    await _requestsBusinessLayer.DeleteRequest(requestId);

                    _logger.LogInformation("request deleted");

                    if (createdTimeSheetIds != null)
                    {
                        foreach (var timeSheetId in createdTimeSheetIds)
                        {
                            _logger.LogInformation($"deleting time sheet {timeSheetId}");

                            await _timeSheetsBusinessLayer.DeleteTimeSheet(timeSheetId);

                            _logger.LogInformation($"time sheet deleted");
                        }
                    }
                }

                throw exc;
            }
        }

        /// <summary>
        /// Получить детализированную заявку
        /// </summary>
        public async Task<DetailedRequestDto> GetRequestAsync(string requestId)
        {
            return await GetDetailedRequest(requestId);
        }

        /// <summary>
        /// Получить все заявки
        /// </summary>
        public async Task<IEnumerable<SimpleRequestDto>> GetRequestsAsync()
        {
            var result = await _requestsBusinessLayer.GetRequests();

            return await GetSimpleRequests(result);
        }

        /// <summary>
        /// Получить заявки по договору
        /// </summary>
        public async Task<IEnumerable<SimpleRequestDto>> GetRequestsByContractAsync(string contractId)
        {
            var result = await _requestsBusinessLayer.GetRequestsByContractId(contractId);

            return await GetSimpleRequests(result);
        }

        /// <summary>
        ///  Получить табели по заявке и указанным наряд заказам
        /// </summary>
        private async Task<IEnumerable<TimeSheetDto>> GetTimeSheets(IEnumerable<string> callOffOrderIds,
            string requestId)
        {
            var result = new List<TimeSheetDto>();

            foreach (var callOffOrderId in callOffOrderIds)
            {
                var callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);

                if (callOffOrder == null)
                {
                    _logger.LogWarning($"callOffOrder with id {callOffOrderId} not found");
                    continue;
                }

                TimeSheet timeSheet =
                    await _timeSheetsBusinessLayer.GetTimeSheetByCallOffOrderAndRequest(callOffOrderId, requestId);

                if (timeSheet == null)
                {
                    _logger.LogWarning(
                        $"Time sheet by call-off order {callOffOrderId} and request {requestId} not found");
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

        /// <summary>
        /// Создать табели для заявки
        /// </summary>
        /// <param name="requestId">ID заявки</param>
        /// <param name="callOffOrderIds">ID наряд заказов</param>
        /// <returns>ID созданных табелей</returns>
        private async Task<IEnumerable<string>> CreateTimeSheetsAsync(string requestId,
            IEnumerable<string> callOffOrderIds)
        {
            var createdTimeSheets = new List<string>();

            string callOffOrderIdsStr = string.Join(",", callOffOrderIds);

            _logger.LogInformation(
                $"creating time sheets for request with id = {requestId} call off orders: {callOffOrderIdsStr}");

            // По каждому наряд заказу создаем табель
            foreach (var callOffOrderId in callOffOrderIds)
            {
                _logger.LogInformation($"step 1. call off order = {callOffOrderId}");

                CallOffOrder callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);

                if (callOffOrder == null)
                {
                    _logger.LogWarning($"call off order {callOffOrderId} not found");
                    continue;
                }

                _logger.LogInformation(
                    $"step 2. startDate = {callOffOrder.StartDate} finishDate = {callOffOrder.FinishDate}");

                var availableRanges = await _timeSheetsBusinessLayer.GetAvailableRanges(null, callOffOrderId,
                    callOffOrder.StartDate, callOffOrder.FinishDate);

                var firstRange = availableRanges.FirstOrDefault();

                if (firstRange == null)
                {
                    _logger.LogWarning(
                        $"no available periods for call off order {callOffOrderId}. Time sheet not created");
                    continue;
                }

                DateTime finishDate = firstRange.Min.AddMonths(1);
                if (finishDate > firstRange.Max)
                    finishDate = firstRange.Max;

                string timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                    firstRange.Min, finishDate, requestId, callOffOrder.CurrencySysName);

                createdTimeSheets.Add(timeSheetId);
            }

            _logger.LogInformation($"created time sheets: {string.Join(",", createdTimeSheets)}");

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
                else
                {
                    var vat = new AmountDto
                    {
                        CurrencySysName = currency,
                        Value = 0
                    };

                    result.Summary.Vats.Add(vat);
                }
            }


            result.StatusName = request.Status.GetName();
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<SimpleRequestDto> GetSimpleRequest(Request request)
        {
            var result = _autoMapper.Map<SimpleRequestDto>(request);
            bool isAdmin = _context.CurrentUser.HasRole(Role.Administrator);

            var documents = await GetTimeSheets(request.CallOffOrderIds, request.Id);

            var requestCurrencies = documents.Select(d => d.CurrencySysName).Distinct();

            var contract = await _contractsBusinessLayer.GetContract(result.ContractId);
            result.ContractNumber = contract.Number;
            result.ContractorName = contract.ContractorName;

            foreach (var currency in requestCurrencies)
            {
                var amount = new AmountDto
                {
                    CurrencySysName = currency,
                    Value = documents.Where(doc => doc.CurrencySysName == currency).Sum(doc => doc.Amount)
                };

                result.Amounts.Add(amount);
            }

            result.StatusName = request.Status.GetName();
            result.StatusSysName = request.Status.ToString();
            result.CanDelete = true;

            if (!isAdmin && request.Status == RequestStatus.Approved)
            {
                result.CanDelete = false;
            }

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
        /// Проверить сумму ппо договору. 
        /// </summary>
        public async Task<CheckAvailableAmountResponse[]> CheckAmountAsync(string requestId)
        {
            var result = new List<CheckAvailableAmountResponse>();

            Request request = await _requestsBusinessLayer.GetRequest(requestId);
            Contract contract = await _contractsBusinessLayer.GetContract(request.ContractId);

            var timeSheets = await _timeSheetsBusinessLayer.GetTimeSheetsByRequestId(requestId);

            var currencies = timeSheets.Select(t => t.CurrencySysName).Distinct();

            foreach (var currency in currencies)
            {
                var availableAmount = new CheckAvailableAmountResponse();

                availableAmount.CurrencySysName = currency;

                var timeSheetsSum = timeSheets
                    .Where(t => t.CurrencySysName.Equals(currency, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.Amount).Sum();

                var contractCurrAmount = contract.Amounts
                    .Where(a => a.CurrencySysName.Equals(currency, StringComparison.OrdinalIgnoreCase)).Single().Value;

                availableAmount.Amount = contractCurrAmount - timeSheetsSum;

                result.Add(availableAmount);
            }

            return result.ToArray();
        }
    }
}