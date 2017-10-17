function fitSize(input) {
    input.size = input.value.length;
}

function stackMessage(text, log) {
    var oldMessage = null;
    if(log.childNodes.length > 0) {
        oldMessage = log.childNodes[0];
        log.removeChild(oldMessage);        
    }

    var message = document.createElement('p');
    message.className = 'message';

    message.innerHTML = text;
    if(oldMessage) {
        message.appendChild(oldMessage);
    }

    log.appendChild(message);
}

window.onload = function() {
    var editor = document.getElementsByTagName('input')[0];
    var log = document.getElementById('log-block');
    
    editor.onkeydown = function(e) {
        if(e.keyCode == 13) {
            stackMessage(editor.value, log);
        }
        fitSize(editor);
    }
    fitSize(editor);
}
