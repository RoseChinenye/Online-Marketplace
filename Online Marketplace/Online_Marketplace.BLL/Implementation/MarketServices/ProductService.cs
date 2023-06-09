﻿using AutoMapper;
using Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Online_Marketplace.BLL.Interface.IMarketServices;
using Online_Marketplace.DAL.Entities;
using Online_Marketplace.DAL.Entities.Models;
using Online_Marketplace.Logger.Logger;
using Online_Marketplace.Shared.DTOs;
using System.Security.Claims;

namespace Online_Marketplace.BLL.Implementation.MarketServices
{

    public class ProductServices : IProductService
    {
        private readonly IMapper _mapper;
        private readonly IRepository<Product> _productRepo;
        private readonly IRepository<Buyer> _buyerRepo;
        private readonly IRepository<Seller> _sellerRepo;
        private readonly IRepository<Cart> _cartRepo;
        private readonly IRepository<OrderItem> _orderitemRepo;
        private readonly IRepository<ProductReviews> _productreivewRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggerManager _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProductServices(IHttpContextAccessor httpContextAccessor, ILoggerManager logger, IUnitOfWork unitOfWork, IMapper mapper)
        {

            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _productRepo = _unitOfWork.GetRepository<Product>();
            _sellerRepo = _unitOfWork.GetRepository<Seller>();
            _cartRepo = _unitOfWork.GetRepository<Cart>();
            _buyerRepo = _unitOfWork.GetRepository<Buyer>();
            _orderitemRepo = _unitOfWork.GetRepository<OrderItem>();
            _productreivewRepo = unitOfWork.GetRepository<ProductReviews>();

        }



        public async Task<string> CreateProduct(ProductCreateDto productDto)
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                throw new Exception("User not found");
            }


            var product = _mapper.Map<Product>(productDto);


            Seller seller = await _sellerRepo.GetSingleByAsync(s => s.UserId == userId);

            if (seller == null)
            {
                throw new Exception("Seller not found");
            }

            product.SellerId = seller.Id;

            await _productRepo.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            return "product created successfully";

        }


        public async Task<string> UpdateProduct(int productId, ProductCreateDto productDto)
        {

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                throw new Exception("User not found");
            }

            var existingProduct = await _productRepo.GetSingleByAsync(x => x.Id == productId, include: x => x.Include(x => x.Seller));

            if (existingProduct == null)
            {
                throw new Exception("Product not found");
            }


            if (existingProduct.Seller.UserId != userId)
            {
                throw new Exception("You do not have permission to update this product");
            }


            var ProductCreateDto = _mapper.Map(productDto, existingProduct);



            await _productRepo.UpdateAsync(ProductCreateDto);
            await _unitOfWork.SaveChangesAsync();

            return "Product updated successfully";

        }

        public async Task<string> DeleteProduct(int productId)
        {

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                throw new Exception("User not found");
            }

            var existingProduct = await _productRepo.GetSingleByAsync(x => x.Id == productId, include: x => x.Include(s => s.Seller));

            if (existingProduct == null)
            {
                throw new Exception("Product not found");
            }


            var buyer = await _sellerRepo.GetSingleByAsync(b => b.UserId == userId);



            if (existingProduct.Seller.UserId != userId)
            {
                throw new Exception("You do not have permission to delete this product");
            }

            await _productRepo.DeleteAsync(existingProduct);
            await _unitOfWork.SaveChangesAsync();

            return "Product deleted successfully";

        }


        public async Task<List<ProductCreateDto>> GetSellerProducts()
        {

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                throw new Exception("User not found");
            }

            Seller seller = await _sellerRepo.GetSingleByAsync(x => x.UserId == userId);

            IEnumerable<Product> sellerPruducts = await _productRepo.GetByAsync(p => p.SellerId == seller.Id);


            return _mapper.Map<List<ProductCreateDto>>(sellerPruducts);
        }



        public async Task<List<ProductCreateDto>> GetProducts(ProductSearchDto searchDto)
        {

            var products = await _productRepo.GetAllAsync();



            if (!string.IsNullOrEmpty(searchDto.Search))
            {
                products = products.Where(p => p.Name.Contains(searchDto.Search, StringComparison.OrdinalIgnoreCase)).ToList();
            }


            var productDtos = _mapper.Map<List<ProductCreateDto>>(products);

            return productDtos;


        }

        public async Task<List<ProductCreateDto>> ViewProducts()
        {
            var products = await _productRepo.GetAllAsync(include: p => p.Include(r => r.ProductReview));


            var productDtos = _mapper.Map<List<ProductCreateDto>>(products);


            return productDtos;

        }

        public async Task<bool> AddToCartAsync(int productId, int quantity)
        {

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var buyer = await _buyerRepo.GetSingleByAsync(b => b.UserId == userId);

            if (buyer == null)
            {
                throw new Exception("Buyer not found");
            }

            var product = await _productRepo.GetByIdAsync(productId);

            if (product == null)
            {
                throw new Exception("Product not found");
            }

            if (quantity <= 0)
            {
                throw new ArgumentException("Invalid quantity.");
            }

            var cart = await _cartRepo.GetSingleByAsync(c => c.BuyerId == buyer.Id, include: q => q.Include(c => c.CartItems));

            if (cart == null)
            {
                cart = new Cart { BuyerId = buyer.Id };
                await _cartRepo.AddAsync(cart);

            }

            if (cart.CartItems == null)
            {
                cart.CartItems = new List<CartItem>();
            }

            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cartItem = new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity
                };
                cart.CartItems.Add(cartItem);
            }

            await _cartRepo.UpdateAsync(cart);

            _logger.LogInfo($"Added product with ID {productId} to cart of buyer with ID {buyer.Id}");

            return true;

        }



        public async Task<string> AddReview(ReviewDto reviewDto)
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var buyer = await _buyerRepo.GetSingleByAsync(b => b.UserId == userId);

            var order = await _orderitemRepo.GetSingleByAsync(c => c.ProductId == reviewDto.ProductId && c.Order.BuyerId == buyer.Id,
                include: oi => oi.Include(oi => oi.Order));

            if (order == null)
            {
                _logger.LogError("Order not found");
                return "Order not found";
            }

            var review = _mapper.Map<ProductReviews>(reviewDto);

            review.BuyerId = buyer.Id;
            review.DateCreated = DateTime.UtcNow;

            await _productreivewRepo.AddAsync(review);

            _logger.LogInfo($"Added review with ID {review.Id}");

            return "Review added successfully";
        }


    }
}
