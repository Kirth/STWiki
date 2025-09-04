using Microsoft.JSInterop;
using STWiki.Services.Interfaces;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Implementation;

/// <summary>
/// Implementation of JavaScript editor service using JSInterop
/// </summary>
public class JavaScriptEditorService : IJavaScriptEditorService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<JavaScriptEditorService> _logger;
    private IJSObjectReference? _editorModule;
    private bool _isDisposed;
    
    public JavaScriptEditorService(IJSRuntime jsRuntime, ILogger<JavaScriptEditorService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }
    
    public async Task InitializeAsync(Guid pageId, string elementId)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            // Load the JavaScript module
            _editorModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/collaborative-editor.js");
            
            // Initialize the editor
            await _editorModule.InvokeVoidAsync("initialize", pageId.ToString(), elementId);
            
            _logger.LogDebug("JavaScript editor initialized for page {PageId}, element {ElementId}", pageId, elementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize JavaScript editor for page {PageId}", pageId);
            throw;
        }
    }
    
    public async Task ApplyRemoteOperationAsync(ITextOperation operation)
    {
        if (_isDisposed || _editorModule == null)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            await _editorModule.InvokeVoidAsync("applyRemoteOperation", operation switch
            {
                InsertOperation insert => new { type = "insert", position = insert.Position, content = insert.Content },
                DeleteOperation delete => new { type = "delete", position = delete.Position, length = delete.Length },
                ReplaceOperation replace => new { type = "replace", start = replace.SelectionStart, end = replace.SelectionEnd, content = replace.NewContent },
                _ => throw new NotSupportedException($"Operation type {operation.GetType().Name} not supported")
            });
            
            _logger.LogDebug("Applied remote operation {OperationId} to JavaScript editor", operation.OperationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply remote operation {OperationId}", operation.OperationId);
            throw;
        }
    }
    
    public async Task<string> GetContentAsync()
    {
        if (_isDisposed || _editorModule == null)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            var content = await _editorModule.InvokeAsync<string>("getContent");
            return content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content from JavaScript editor");
            throw;
        }
    }
    
    public async Task SetContentAsync(string content)
    {
        if (_isDisposed || _editorModule == null)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            await _editorModule.InvokeVoidAsync("setContent", content ?? string.Empty);
            _logger.LogDebug("Set content in JavaScript editor: {Length} characters", content?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set content in JavaScript editor");
            throw;
        }
    }
    
    public async Task<(int start, int end)> GetSelectionAsync()
    {
        if (_isDisposed || _editorModule == null)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            var selection = await _editorModule.InvokeAsync<int[]>("getSelection");
            return (selection[0], selection[1]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get selection from JavaScript editor");
            throw;
        }
    }
    
    public async Task SetSelectionAsync(int start, int end)
    {
        if (_isDisposed || _editorModule == null)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            await _editorModule.InvokeVoidAsync("setSelection", start, end);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set selection in JavaScript editor");
            throw;
        }
    }
    
    public async Task ShowRemoteCursorAsync(string userId, string userColor, int position, int selectionEnd)
    {
        if (_isDisposed || _editorModule == null)
            throw new ObjectDisposedException(nameof(JavaScriptEditorService));
            
        try
        {
            await _editorModule.InvokeVoidAsync("showRemoteCursor", userId, userColor, position, selectionEnd);
            _logger.LogDebug("Showed remote cursor for user {UserId} at {Position}-{SelectionEnd}", 
                userId, position, selectionEnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show remote cursor for user {UserId}", userId);
            // Don't throw for cursor display errors
        }
    }
    
    public async Task HideRemoteCursorAsync(string userId)
    {
        if (_isDisposed || _editorModule == null)
            return; // Already disposed or not initialized
            
        try
        {
            await _editorModule.InvokeVoidAsync("hideRemoteCursor", userId);
            _logger.LogDebug("Hid remote cursor for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hide remote cursor for user {UserId}", userId);
            // Don't throw for cursor display errors
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
            
        try
        {
            if (_editorModule != null)
            {
                await _editorModule.InvokeVoidAsync("dispose");
                await _editorModule.DisposeAsync();
                _editorModule = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing JavaScript editor service");
        }
        finally
        {
            _isDisposed = true;
        }
    }
}